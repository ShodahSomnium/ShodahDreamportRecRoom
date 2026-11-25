#!/usr/bin/perl
# isle_thin.pl - fits one gtscene jt=1 RRSK of an Isle of Lost Skulls level into the quest's data budget (iteration 13,
# isle agent). The 2018 pirate levels are mostly curved art: sand dunes, coral, rocks, palm crowns, rope coils. gtscene
# jt=1 writes them as one fitted rectangle per triangle pair (Beach1 164 k "G plate", SunkenShip 660 k 0.2 m "C box"
# collider plates from the level's own mesh colliders), 3.5-60 MB per room, where the whole quest may have ~3 MB gzip.
#
# What it does (nothing else in the file changes):
#   G plate  small plates (area < S^2) of one material that share a cell of S metres, a normal bin (cube face + 3 x 3,
#            ~30 degrees) and a plane-offset slot become ONE plate on their area-weighted mean plane: the rectangle of
#            all their corners on the biggest plate's axes, shrunk to at most 1.3 x their summed area (sparse leaves stay
#            sparse), the biggest plate's vertex colour. Plates of at least S^2 stay exact (walls, decks, floors).
#            S grows (0.35 m .. 4 m, then 2.7-6 m with 6 coarse normal bins) until the room has at most <plates> plates;
#            materials with fewer than 400 plates (the level's own props) use half that S; if still over, the smallest
#            plates (< 0.03 m2) are left out.
#   C box    the 0.2 m thick collider plates (size z == 0.2, gtscene's mesh-collider plates) the same way into at most
#            <boxes> boxes (no area shrink below 2.5 x, 4 cm overlap each side so merged floors have no seams).
# Reference data only: measured numbers in, measured numbers out.
#
#   perl isle_thin.pl <in.rrsk> <out.rrsk> [plates=18000] [boxes=4000]
use strict; use warnings;
my ($in, $out, @opt) = @ARGV;
die "usage: isle_thin.pl <in.rrsk> <out.rrsk> [plates=N] [boxes=N]\n" unless $in && $out;
my %o = (plates => 18000, boxes => 4000);
for (@opt) { my ($k, $v) = split /=/, $_, 2; $o{$k} = $v; }

open(my $h, '<', $in) or die "$in: $!\n";
my @lines = <$h>;
close $h;
s/\r?\n$// for @lines;

# ---------------------------------------------------------------- read
my (@P, @C);   # [line index, mat, cx,cy,cz, qx,qy,qz,qw, hx,hy, rest, heavy]
my %matCount;
for my $i (0 .. $#lines) {
  my $l = $lines[$i];
  if (substr($l, 0, 8) eq 'G plate ') {
    my @f = split / /, $l;
    my ($e) = grep { substr($_, 0, 2) eq 'e=' } @f[10 .. $#f];
    next unless defined $e;
    my ($hx, $hy) = split /,/, substr($e, 2);
    my @rest = grep { substr($_, 0, 2) ne 'e=' } @f[10 .. $#f];
    push @P, [$i, $f[2], @f[3 .. 9], $hx, $hy, join(' ', @rest)];
    $matCount{$f[2]}++;
  } elsif (substr($l, 0, 6) eq 'C box ') {
    my @f = split / /, $l;
    next unless @f == 12 && $f[11] == 0.2;
    push @C, [$i, '', @f[2 .. 8], $f[9] / 2, $f[10] / 2, ''];
  }
}

# ---------------------------------------------------------------- math
sub axes {
  my ($x, $y, $z, $w) = @_;
  return ([1 - 2 * ($y * $y + $z * $z), 2 * ($x * $y + $w * $z), 2 * ($x * $z - $w * $y)],
          [2 * ($x * $y - $w * $z), 1 - 2 * ($x * $x + $z * $z), 2 * ($y * $z + $w * $x)],
          [2 * ($x * $z + $w * $y), 2 * ($y * $z - $w * $x), 1 - 2 * ($x * $x + $y * $y)]);
}
sub dot { return $_[0][0] * $_[1][0] + $_[0][1] * $_[1][1] + $_[0][2] * $_[1][2]; }
sub cross { my ($a, $b) = @_; return [$a->[1] * $b->[2] - $a->[2] * $b->[1], $a->[2] * $b->[0] - $a->[0] * $b->[2], $a->[0] * $b->[1] - $a->[1] * $b->[0]]; }
sub norm { my $v = shift; my $l = sqrt(dot($v, $v)); return $l < 1e-9 ? undef : [$v->[0] / $l, $v->[1] / $l, $v->[2] / $l]; }
sub quat {    # columns ax, ay, az -> (x, y, z, w)
  my ($a, $b, $c) = @_;
  my ($m00, $m10, $m20, $m01, $m11, $m21, $m02, $m12, $m22) = (@$a, @$b, @$c);
  my $tr = $m00 + $m11 + $m22; my ($x, $y, $z, $w);
  if ($tr > 0) { my $s = sqrt($tr + 1) * 2; $w = 0.25 * $s; $x = ($m21 - $m12) / $s; $y = ($m02 - $m20) / $s; $z = ($m10 - $m01) / $s; }
  elsif ($m00 > $m11 && $m00 > $m22) { my $s = sqrt(1 + $m00 - $m11 - $m22) * 2; $w = ($m21 - $m12) / $s; $x = 0.25 * $s; $y = ($m01 + $m10) / $s; $z = ($m02 + $m20) / $s; }
  elsif ($m11 > $m22) { my $s = sqrt(1 + $m11 - $m00 - $m22) * 2; $w = ($m02 - $m20) / $s; $x = ($m01 + $m10) / $s; $y = 0.25 * $s; $z = ($m12 + $m21) / $s; }
  else { my $s = sqrt(1 + $m22 - $m00 - $m11) * 2; $w = ($m10 - $m01) / $s; $x = ($m02 + $m20) / $s; $y = ($m12 + $m21) / $s; $z = 0.25 * $s; }
  return ($x, $y, $z, $w);
}
sub num { my ($v, $d) = @_; $v = sprintf("%.${d}f", $v); $v =~ s/0+$// if $v =~ /\./; $v =~ s/\.$//; $v = '0' if $v eq '-0' || $v eq ''; return $v; }

# normal bin: dominant axis and sign (6) x 3 x 3 on that face; the bin's centre direction for the plane offset
sub nbin {
  my ($n, $coarse) = @_; my @a = map { abs } @$n;
  if ($coarse) {
    my $k = $a[0] >= $a[1] && $a[0] >= $a[2] ? 0 : $a[1] >= $a[2] ? 1 : 2;
    my @c = (0, 0, 0); $c[$k] = $n->[$k] >= 0 ? 1 : -1;
    return (100 + $k * 2 + ($n->[$k] >= 0 ? 1 : 0), \@c);
  }
  my $k = $a[0] >= $a[1] && $a[0] >= $a[2] ? 0 : $a[1] >= $a[2] ? 1 : 2;
  my ($u, $v) = grep { $_ != $k } 0 .. 2;
  my $m = $a[$k] || 1e-9;
  my $bu = int(($n->[$u] / $m + 1) * 1.5); $bu = 2 if $bu > 2;
  my $bv = int(($n->[$v] / $m + 1) * 1.5); $bv = 2 if $bv > 2;
  my $s = $n->[$k] >= 0 ? 1 : -1;
  my @c = (0, 0, 0); $c[$k] = $s; $c[$u] = ($bu - 1) * 0.667; $c[$v] = ($bv - 1) * 0.667;
  return (($k * 2 + ($s > 0 ? 1 : 0)) * 9 + $bu * 3 + $bv, norm(\@c));
}

# precompute axes and area per record
for my $r (@P, @C) {
  my ($ax, $ay, $az) = axes(@$r[5 .. 8]);
  my ($bin, $bc) = nbin($az, 0);
  my ($cbin, $cbc) = nbin($az, 1);
  push @$r, $ax, $ay, $az, $bin, $bc, 4 * $r->[9] * $r->[10], $cbin, $cbc;    # 12..19
}

# ---------------------------------------------------------------- cluster
# returns (count after, map of line index -> replacement line or '' for dropped)
sub cluster {
  my ($recs, $step, $isBox, $small) = @_;
  # a step "cN": N metres with the coarse normal bins (6 faces, for foliage and rock that is still over budget)
  my $coarse = $step =~ s/^c//; my $S = $step;
  my %b;
  for my $r (@$recs) {
    my $s = $isBox ? $S : ($matCount{$r->[1]} < $small ? $S * 0.5 : $S);
    next if $s < 0.2 || $r->[17] >= $s * $s;
    my $sd = $s * 0.3; $sd = 0.12 if $sd < 0.12;
    my ($bi, $bn) = $coarse ? ($r->[18], $r->[19]) : ($r->[15], $r->[16]);
    my $off = int(POSIX::floor(dot($bn, [@$r[2 .. 4]]) / $sd));
    my $key = join(',', $r->[1], $bi, $off, map { POSIX::floor($_ / $s) } @$r[2 .. 4]);
    push @{ $b{$key} }, $r;
  }
  my %rep; my $n = scalar(@$recs);
  for my $g (values %b) {
    next if @$g < 2;
    $n -= @$g - 1;
    my ($best) = sort { $b->[17] <=> $a->[17] } @$g;
    my $sa = 0; my @nn = (0, 0, 0); my @cc = (0, 0, 0);
    for my $r (@$g) { my $a = $r->[17] + 1e-6; $sa += $a; $nn[$_] += $a * $r->[14][$_] for 0 .. 2; $cc[$_] += $a * $r->[2 + $_] for 0 .. 2; }
    my $nz = norm(\@nn) || $best->[14];
    my $c0 = [map { $_ / $sa } @cc];
    my $ax = norm([map { $best->[12][$_] - dot($best->[12], $nz) * $nz->[$_] } 0 .. 2]) || norm(cross($nz, [0, 1, 0])) || [1, 0, 0];
    my $ay = cross($nz, $ax);
    my ($u0, $u1, $v0, $v1, $as) = (1e9, -1e9, 1e9, -1e9, 0);
    for my $r (@$g) {
      $as += $r->[17] * abs(dot($r->[14], $nz));
      for my $su (-1, 1) { for my $sv (-1, 1) {
        my @p = map { $r->[2 + $_] + $su * $r->[9] * $r->[12][$_] + $sv * $r->[10] * $r->[13][$_] - $c0->[$_] } 0 .. 2;
        my $u = dot(\@p, $ax); my $v = dot(\@p, $ay);
        $u0 = $u if $u < $u0; $u1 = $u if $u > $u1; $v0 = $v if $v < $v0; $v1 = $v if $v > $v1;
      } }
    }
    my ($hx, $hy) = (($u1 - $u0) / 2, ($v1 - $v0) / 2);
    my @c = map { $c0->[$_] + $ax->[$_] * ($u0 + $u1) / 2 + $ay->[$_] * ($v0 + $v1) / 2 } 0 .. 2;
    my $K = $isBox ? 2.5 : 1.3; my $ab = 4 * $hx * $hy;
    if ($ab > $K * $as && $ab > 0) { my $f = sqrt($K * $as / $ab); $hx *= $f; $hy *= $f; }
    my @q = quat($ax, $ay, $nz);
    my $pos = join(' ', map { num($_, 4) } @c) . ' ' . join(' ', map { num($_, 4) } @q);
    my $line;
    if ($isBox) { $line = "C box $pos " . num(2 * $hx + 0.08, 4) . ' ' . num(2 * $hy + 0.08, 4) . ' 0.2'; }
    else {
      $line = "G plate $best->[1] $pos e=" . num($hx, 4) . ',' . num($hy, 4);
      $line .= " $best->[11]" if length $best->[11];
    }
    $rep{$best->[0]} = $line;
    $rep{$_->[0]} = '' for grep { $_ != $best } @$g;
  }
  return ($n, \%rep);
}

use POSIX ();
my %all;
my @steps = (0.35, 0.5, 0.7, 0.9, 1.15, 1.45, 1.8, 2.2, 2.7, 3.3, 4.0, 'c2.7', 'c3.3', 'c4', 'c5', 'c6');
my ($pS, $pN, $cS, $cN, $tiny, $tinyN) = (0, scalar(@P), 0, scalar(@C), 0, 0);
if (@P > $o{plates}) {
  my $rep;
  for my $S (@steps) {
    ($pN, $rep) = cluster(\@P, $S, 0, 400);
    $pS = $S;
    last if $pN <= $o{plates};
  }
  %all = (%all, %$rep);
  # still over: the smallest plates go (props' 1-10 cm facets), from 0.002 m2 up to at most 0.03 m2
  if ($pN > $o{plates}) {
    my @areas;
    for my $r (@P) {
      if (exists $all{$r->[0]}) {
        next unless length $all{$r->[0]};
        my ($hx, $hy) = $all{$r->[0]} =~ / e=([-0-9.]+),([-0-9.]+)/;
        push @areas, [$r->[0], 4 * $hx * $hy];
      } else { push @areas, [$r->[0], $r->[17]]; }
    }
    @areas = sort { $a->[1] <=> $b->[1] } @areas;
    my $cut = $pN - $o{plates};
    for my $x (@areas) {
      last if $cut <= 0 || $x->[1] >= 0.03;
      $all{$x->[0]} = ''; $cut--; $pN--; $tinyN++; $tiny = $x->[1];
    }
  }
}
if (@C > $o{boxes}) {
  my $rep;
  for my $S (@steps) {
    ($cN, $rep) = cluster(\@C, $S, 1, 0);
    $cS = $S;
    last if $cN <= $o{boxes};
  }
  %all = (%all, %$rep);
}

open(my $w, '>', $out) or die "$out: $!\n";
my $noted = 0;
for my $i (0 .. $#lines) {
  if (exists $all{$i}) { print $w "$all{$i}\n" if length $all{$i}; next; }
  print $w "$lines[$i]\n";
  if (!$noted && $lines[$i] =~ /^# gtscene jt=1: stand-ins/) {
    printf $w "# isle_thin (DevNotes/iter13/isle_tools/isle_thin.pl): hull plates %d -> %d (cell %s m; %d smallest < %.4f m2 left out), collider plates %d -> %d (cell %s m)\n", scalar(@P), $pN, $pS, $tinyN, $tiny, scalar(@C), $cN, $cS;
    $noted = 1;
  }
}
close $w;
printf "%s: plates %d -> %d (S %s, %d tiny out < %.4f m2), collider plates %d -> %d (S %s)\n", $out, scalar(@P), $pN, $pS, $tinyN, $tiny, scalar(@C), $cN, $cS;
