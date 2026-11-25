#!/usr/bin/perl
# dg_terraincol.pl - iteration 15 Disc Golf: the Disc Golf levels' ground is one Unity terrain with its collider over the
# whole 256 m course, but apkscene wrote it as a near grid (2 m, collider=1) around the pregame lobby only and a far grid
# (4 m, collider=0, far=1: drawn 8 cm low, never solid) for the rest, so most holes had nothing to stand on (Lake: 42 %
# of the course's 2 m cells solid). This adds the far grid's heights again as collider-only grids (vis=0, collider=1,
# not far; 0.08 m lower, where RrSceneKitTerrain draws a far grid: FarDrop) over the parts of it the near grid does not cover: up to four rectangles (south / north strips, west / east
# of the near grid), each overlapping the near grid by one 4 m cell so there is no seam. Measured numbers only.
#   perl dg_terraincol.pl <in.rrsk> <out.rrsk>
use strict; use warnings;
my ($in, $out) = @ARGV; die "usage: dg_terraincol.pl in out\n" unless $in && $out;
open(my $h, '<', $in) or die "$in: $!\n"; my @lines = <$h>; close $h;
my (%grid, %rows);
for (@lines) {
  next unless /^H /; my @a = split;
  if ($a[1] eq 'grid') { $grid{$a[2]} = [@a]; }
  elsif ($a[1] eq 'row') { $rows{$a[2]}[$a[3]] = [@a[4 .. $#a]]; }
}
my @add;
for my $far (sort keys %grid) {
  next unless $far =~ /^(.*)~far#\d+$/; my $base = $1;
  my ($near) = grep { /^\Q$base\E#\d+$/ && $grid{$_}->[-1] ne '' && "@{$grid{$_}}" =~ /collider=1/ } keys %grid;
  my @f = @{$grid{$far}}; my ($ox,$oy,$oz,$dx,$dz,$nx,$nz) = @f[3 .. 9];
  my @R = @{$rows{$far}};
  my ($i0,$i1,$j0,$j1) = (0, -1, 0, -1);   # near rect in far cell indices (inclusive vertex ranges covered)
  if ($near) { my @n = @{$grid{$near}};
    my ($nox,$noz) = ($n[3],$n[5]); my ($nx1,$nz1) = ($nox + $n[6]*($n[8]-1), $noz + $n[7]*($n[9]-1));
    $i0 = int(($nox - $ox)/$dx) + 1; $i1 = int(($nx1 - $ox)/$dx);   # vertices strictly inside the near rect
    $j0 = int(($noz - $oz)/$dz) + 1; $j1 = int(($nz1 - $oz)/$dz); }
  my @rects;   # [ia, ib, ja, jb] vertex index ranges (inclusive)
  if ($i1 >= $i0 && $j1 >= $j0) {
    push @rects, [0, $nx-1, 0, $j0] if $j0 > 0;            # south strip (to the near grid's first inside row)
    push @rects, [0, $nx-1, $j1, $nz-1] if $j1 < $nz-1;      # north strip
    push @rects, [0, $i0, $j0, $j1] if $i0 > 0;              # west
    push @rects, [$i1, $nx-1, $j0, $j1] if $i1 < $nx-1;      # east
  } else { push @rects, [0, $nx-1, 0, $nz-1]; }
  my $k = 10;
  for my $r (@rects) { my ($ia,$ib,$ja,$jb) = @$r; next if $ib - $ia < 1 || $jb - $ja < 1;
    my $name = $base . "Collider#" . $k++;
    my $kv = join(' ', grep { !/^(far|vis|collider)=/ } @f[10 .. $#f]);
    push @add, sprintf("H grid %s %s %s %s %s %s %d %d %s collider=1 vis=0\n", $name, $ox + $ia*$dx, $oy - 0.08, $oz + $ja*$dz, $dx, $dz, $ib-$ia+1, $jb-$ja+1, $kv);
    for my $j ($ja .. $jb) { push @add, "H row $name " . ($j-$ja) . " " . join(' ', @{$R[$j]}[$ia .. $ib]) . "\n"; }
    printf STDERR "%s: %s from %s: %d x %d vertices at x %.1f..%.1f z %.1f..%.1f\n", $out, $name, $far, $ib-$ia+1, $jb-$ja+1, $ox+$ia*$dx, $ox+$ib*$dx, $oz+$ja*$dz, $oz+$jb*$dz;
  }
}
open(my $o, '>', $out) or die; my $done = 0;
for (@lines) { print $o $_; }
print $o @add; close $o;
