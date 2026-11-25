#!/usr/bin/perl
# cbm_trim.pl - fits a gtscene jt=1 RRSK of a Crescendo of the Blood Moon level (DN/refdata/apk/Crescendo_<Room>.rrsk)
# into the game's data budget (~3 MB of gzip for 11 rooms; the raw levels are 17 MB) and gives the level-only
# materials Rec Room's measured mean colours. Numbers only; nothing of the Android build is copied.
#
#   perl cbm_trim.pl <in.rrsk> <out.rrsk> <texavg.txt> [amin=0.03] [cell=2] [fmin=1.5]
#
# - Foliage (leaf cards of the mobile oak / pine / bush meshes, brambles, hedges, the flat spooky-tree standees: 60-80 %
#   of every outdoor level's plates) becomes coarse canopy boxes: the plate centres binned into <cell> m cells, one
#   "G dress" box per cell holding at least <fmin> m2 of leaves, at the measured mean colour of the level texture.
# - Other plates under <amin> m2 are left out (tiny trims: ~4 % of the area, ~70 % of the plates).
# - Not-surfaces (fog cards, light shafts, moonlight beams, glow cards) are left out.
# - Level-only materials with a white _BaseColor get col = the texture's effective mean albedo (AG/Standard math:
#   albedo_lin = m0 + m1 * lin(_BaseColor), texavg's m0lin / m1lin), so RrJumbotronLook's paint fallback shows Rec Room's
#   colour instead of a flat grey. Wood / concrete / metal names keep theirs (they get an install template texture).
use strict; use warnings;
my ($in, $out, $tav, $amin, $cell, $fmin) = @ARGV;
die "usage: cbm_trim.pl in out texavg [amin] [cell] [fmin]\n" unless $in && $out && $tav;
$amin //= 0.03; $cell //= 2; $fmin //= 1.5;
sub lin { my $c = shift; $c <= 0.04045 ? $c / 12.92 : (($c + 0.055) / 1.055) ** 2.4 }
sub gam { my $c = shift; $c = 0 if $c < 0; $c = 1 if $c > 1; $c <= 0.0031308 ? 12.92 * $c : 1.055 * $c ** (1 / 2.4) - 0.055 }
my %tex;
open(my $t, '<', $tav) or die "$tav: $!\n";
while (<$t>) {
  chomp; my ($m, $slot, $r) = split /\t/;
  next unless defined $r && ($slot eq '_BaseMap' || ($slot eq '_MainTex' && !exists $tex{$m}));
  $tex{$m} = [$1, $2, $3, $4, $5, $6] if $r =~ /m1lin (\S+),(\S+),(\S+) m0lin (\S+),(\S+),(\S+)/;
}
close $t;
my $FOL = qr/Tree_(Oak|Pine)|Tree_Bush|Bush_Mobile|Brambles|Hedges|SpookyTree|Cutout|Ivy|Vine|Leaves|Foliage|Fern|PumpkinPlant|Canopy/i;
my $TRUNK = qr/trunk/i;
# iteration 13 (crescendo, 2nd pass): "Beam" matched SupportBeams_Mat (the wooden beams: 7 k plates in the Township and the
# Library) and "Glow" TowerFacade_GlowingWindows_Mat (the castle towers): both are surfaces and stay
my $SKIP = qr/Fog|LightShaft|Lightshaft|Moonlight|GodRay|Glow(?!ing)|LightBeam|Mist|Haze|Portal_|Nav_Mat|InvisibleWall|DoorTrigger|Shadow_Blob|Decal/i;
my (%col, %inst, %cells, %drop, $kept, $small, $fol, $skipped);
open(my $h, '<', $in) or die "$in: $!\n";
open(my $o, '>', $out) or die "$out: $!\n";
my @body;
while (my $l = <$h>) {
  $l =~ s/\r?\n$//;
  my @f = split / /, $l;
  if (@f > 2 && $f[0] eq 'G' && $f[1] eq 'platemat') {
    my $m = $f[2];
    my ($c) = $l =~ / col=(\S+)/; my ($i) = $l =~ / inst=(\d)/;
    $inst{$m} = $i // 0;
    my @c = split /,/, ($c // '1,1,1,1');
    $c[3] //= 1;
    if (!$inst{$m} && $m !~ /^(concrete|metal|wood)/i && exists $tex{$m} && $c[0] >= 0.97 && $c[1] >= 0.97 && $c[2] >= 0.97) {
      my $x = $tex{$m};
      @c[0 .. 2] = map { gam($x->[3 + $_] + $x->[$_] * lin($c[$_])) } 0 .. 2;
      my $nc = join(',', map { sprintf('%.3f', $_) } @c[0 .. 2]) . ',' . $c[3];
      $l =~ s/ col=\S+/ col=$nc/;
    }
    $col{$m} = [@c];
    push @body, $l; next;
  }
  if (@f > 2 && $f[0] eq 'G' && $f[1] eq 'plate') {
    my $m = $f[2];
    if ($m =~ $SKIP) { $skipped++; $drop{$m}++; next; }
    my ($hx, $hy) = $l =~ / e=([0-9.eE+-]+),([0-9.eE+-]+)/;
    next unless defined $hy;
    my $a = 4 * $hx * $hy;
    if ($m =~ $FOL && $m !~ $TRUNK) {
      my ($x, $y, $z) = @f[3 .. 5];
      my $key = join(',', $m, map { int(($_ < 0 ? $_ - $cell : $_) / $cell) } ($x, $y, $z));
      my $cc = $cells{$key} //= { m => $m, a => 0, lo => [$x, $y, $z], hi => [$x, $y, $z] };
      $cc->{a} += $a;
      for my $k (0 .. 2) { my $v = ($x, $y, $z)[$k]; $cc->{lo}[$k] = $v if $v < $cc->{lo}[$k]; $cc->{hi}[$k] = $v if $v > $cc->{hi}[$k]; }
      $fol++; next;
    }
    if ($a < $amin) { $small++; next; }
    $kept++; push @body, $l; next;
  }
  push @body, $l;
}
close $h;
# foliage colours: the texture's effective mean (its cut-out background counts too: darkened towards Rec Room's leaves)
my %fixed = (
  'Tree_Oak_Mobile_Yellow_mat' => [0.78, 0.55, 0.17], 'RecRoyale_Tree_Oak_Yellow_mat' => [0.78, 0.55, 0.17],
  'Tree_Pine_Double_Sided_Mat' => [0.17, 0.3, 0.2], 'Tree_Pine_Mobile_Double_Sided_Mat' => [0.17, 0.3, 0.2],
  'SpookyTree_Cutout_Mat' => [0.14, 0.11, 0.15], 'Brambles_Cutout_Mat' => [0.22, 0.15, 0.13]
);
my $boxes = 0;
for my $key (sort keys %cells) {
  my $cc = $cells{$key};
  next if $cc->{a} < $fmin;
  my @c = @{ $fixed{$cc->{m}} // $col{$cc->{m}} // [0.25, 0.4, 0.2] };
  my @ctr = map { ($cc->{lo}[$_] + $cc->{hi}[$_]) / 2 } 0 .. 2;
  my @e = map { my $v = ($cc->{hi}[$_] - $cc->{lo}[$_]) / 2 + 0.3; $v = $cell / 2 if $v > $cell / 2; $v < 0.35 ? 0.35 : $v } 0 .. 2;
  my $mm = ($inst{$cc->{m}} ? '' : '?') . $cc->{m};
  push @body, sprintf('G dress Canopy_%s %.3f %.3f %.3f 0 0 0 1 e=%.3f,%.3f,%.3f c=%.3f,%.3f,%.3f,1 m=%s', $cc->{m}, @ctr, @e, @c[0 .. 2], $mm);
  $boxes++;
}
print $o "$_\n" for @body;
close $o;
printf STDERR "%s: plates kept %d, small dropped %d (< %s m2), not-surfaces %d, foliage %d -> %d canopy boxes (%s m cells)\n", $in, $kept // 0, $small // 0, $amin, $skipped // 0, $fol // 0, $boxes, $cell;
