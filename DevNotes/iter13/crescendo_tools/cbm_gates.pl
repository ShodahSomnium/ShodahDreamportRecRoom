#!/usr/bin/perl
# cbm_gates.pl - Crescendo of the Blood Moon's mid-level gates in the RRSK (iteration 13, crescendo agent). Each
# [Quest_Dracula1_MidLevelGate] has an AreaBlockingGeo box collider (a 9 x 27 x 11-18 m invisible wall over the area
# behind the gate) that Rec Room removes when the gate opens, and an iron door (mesh fence_midgate, drawn by gtscene as
# Candleabras_mat plates) that swings open. Both are taken out of the level data here, from the gate_block / gate_door
# markers cbm_markers.pl appended: the collider record whose centre and size match a gate_block, and the door's plates
# (Candleabras_mat, centre within the door's own box: 0.35 m deep, the hinge-to-centre half width, 2.9 m x scale tall).
# The quest builds both back as its own objects (RrCrescendoGate: the blocker is on until the gate's waves are beaten,
# the door bars rise). Names and numbers only.
#   perl cbm_gates.pl in.rrsk > out.rrsk
use strict; use warnings;
my $f = shift or die "usage: cbm_gates.pl <in.rrsk>\n";
my (@blocks, @doors);
open(my $h, '<', $f) or die "$f: $!\n";
while (<$h>) {
  my @t = split / /;
  if ($t[0] eq 'G' && $t[1] eq 'gate_block') { my ($s) = / size=(\S+)/; push @blocks, [@t[3 .. 5], split(/,/, $s)]; }
  if ($t[0] eq 'G' && $t[1] eq 'gate_door') { my ($sc) = / scale=([^, ]+)/; my ($hg) = / hinge=(\S+)/; push @doors, [@t[3 .. 9], $sc // 1, $hg ? split(/,/, $hg) : @t[3 .. 5]]; }
}
close $h;
sub inv { my ($q, $v) = @_; my ($x, $y, $z, $w) = (-$q->[0], -$q->[1], -$q->[2], $q->[3]);
  my @t = (2 * ($y * $v->[2] - $z * $v->[1]), 2 * ($z * $v->[0] - $x * $v->[2]), 2 * ($x * $v->[1] - $y * $v->[0]));
  return ($v->[0] + $w * $t[0] + ($y * $t[2] - $z * $t[1]), $v->[1] + $w * $t[1] + ($z * $t[0] - $x * $t[2]), $v->[2] + $w * $t[2] + ($x * $t[1] - $y * $t[0])); }
my ($nc, $np) = (0, 0);
open($h, '<', $f) or die "$f: $!\n";
while (my $l = <$h>) {
  my @t = split / /, $l;
  if ($t[0] eq 'C' && $t[1] eq 'box' && @t >= 12) {
    my $drop = 0;
    for my $k (@blocks) {
      my $d = abs($t[2] - $k->[0]) + abs($t[3] - $k->[1]) + abs($t[4] - $k->[2]);
      my @s = sort { $a <=> $b } @t[9 .. 11]; my @bs = sort { $a <=> $b } @$k[3 .. 5];
      if ($d < 0.08 && abs($s[0] - $bs[0]) < 0.05 * $bs[0] + 0.02 && abs($s[2] - $bs[2]) < 0.05 * $bs[2] + 0.02) { $drop = 1; last; }
    }
    if ($drop) { $nc++; next; }
  }
  if ($t[0] eq 'G' && $t[1] eq 'plate' && $t[2] eq 'Candleabras_mat') {
    my $drop = 0;
    for my $d (@doors) {
      my @q = @$d[3 .. 6]; my $sc = $d->[7]; my @hg = @$d[8 .. 10];
      my @lp = inv(\@q, [$t[3] - $d->[0], $t[4] - $d->[1], $t[5] - $d->[2]]);
      my @lh = inv(\@q, [$hg[0] - $d->[0], $hg[1] - $d->[1], $hg[2] - $d->[2]]);
      my $hw = abs($lh[2]) > 0.3 ? abs($lh[2]) : 1.5 * $sc;
      if (abs($lp[0]) < 0.35 && abs($lp[2]) < $hw + 0.1 && $t[4] > $hg[1] - 0.2 && $t[4] < $hg[1] + 2.9 * $sc) { $drop = 1; last; }
    }
    if ($drop) { $np++; next; }
  }
  print $l;
}
close $h;
print STDERR "cbm_gates: " . scalar(@blocks) . " gates: $nc blocking colliders and $np door plates left out\n";
