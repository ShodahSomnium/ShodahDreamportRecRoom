#!/usr/bin/perl
# cbm_markers.pl - extra G markers for Crescendo of the Blood Moon's RRSK (iteration 13, crescendo agent), after
# DN/iter8/jumbotron_markers.pl. The Dracula1 levels drive their fights from plain Transforms under
# Systems/Encounters, Systems/RequiredEncounters, Systems/OptionalEncounters (the Dungeon: NewGeo/Encounters,
# Dungeon/Encounters): per encounter "Spawn Points" / "ExplicitSpawn(s)" (points), "Spawn Regions" / "Patrol Regions"
# (boxes: the world SCALE is the size) and "Triggers/Overlap Volume" (the zone that starts it, scale = size). The gather
# pads [DoorTrigger] / [TempDoorTrigger] (exit and mid-level gates), the silver chests [Quest_Dracula1_CollectablesChest],
# the three light mirrors [Quest_Dracula1_MirrorCannon] and the Dungeon's traps are printed too. Names and numbers only.
#
# Input: the broad apkscene find dump (DN/iter8/jumbotron_tools/make_tools.sh "apks": script "-" = every GameObject,
# columns "path | pos | rot | act= | scripts | s=<world scale> | comps=<n> | colliders").
#   perl cbm_markers.pl levelN.txt >> Crescendo_<Room>.rrsk
use strict; use warnings;
my $f = shift or die "usage: cbm_markers.pl <dump.txt>\n";
open(my $h, '<', $f) or die "$f: $!\n";
my (@rows, %isParent, %byPath);
while (my $l = <$h>) {
  chomp $l; $l =~ s/\r$//;
  my @c = split / \| /, $l, -1;
  next if @c < 5 || $c[1] !~ /^-?[0-9]/;
  my ($path, $pos, $rot, $act, $scr, $sc, $comps, $cols) = @c;
  $path =~ s/ +$//;
  $sc = defined $sc ? $sc : 's=1 1 1'; $sc =~ s/^s=//;
  push @rows, [$path, $pos, $rot, $act, $scr // '', $sc, $cols // ''];
  $byPath{$path} //= $rows[-1];
  my @seg = split m{/}, $path;
  for my $i (1 .. $#seg) { $isParent{join('/', @seg[0 .. $i - 1])} = 1; }
}
close $h;
sub enc { my $s = shift; $s =~ s/%/%25/g; $s =~ s/ /%20/g; return $s; }
sub num { my $v = shift; $v = sprintf('%.4f', $v); $v =~ s/0+$//; $v =~ s/\.$//; $v = '0' if $v eq '-0' || $v eq ''; return $v; }
sub trs { my ($p, $r) = @_; return join(' ', map { num($_) } (split(/ /, $p), split(/ /, $r))); }
sub size { my $s = shift; return join(',', map { num(abs($_)) } split / /, $s); }
my %seen; my %n;
sub out {
  my ($kind, $name, $row, @kv) = @_;
  my $key = "$kind|$row->[0]|$row->[1]";
  return if $seen{$key}++;
  my $act = $row->[3] =~ /True/ ? 1 : 0;
  my $p = $row->[0]; $p = substr($p, -110) if length $p > 110;
  print join(' ', 'G', $kind, enc($name), trs($row->[1], $row->[2]), @kv, "active=$act", 'path=' . enc($p)), "\n";
  $n{$kind}++;
}
for my $r (@rows) {
  my ($path, $pos, $rot, $act, $scr, $sc) = @$r;
  my @seg = split m{/}, $path;
  my $leaf = $seg[-1];
  my $isLeaf = !$isParent{$path};
  my $ei = -1;
  for my $i (0 .. $#seg) { if ($seg[$i] =~ /^((Required|Optional)?Encounters|EncountersV2)$/) { $ei = $i; last; } }
  my $encName = $ei >= 0 && $ei + 1 <= $#seg ? $seg[$ei + 1] : '';
  # the mid-level gates: the door (LockableDoor: pose, world scale; its mesh pivot "fbx" is the door's hinge edge) and the
  # AreaBlockingGeo box collider behind it (the invisible wall Rec Room removes when the gate opens), as world boxes
  if ($path =~ m{/(\[Quest_Dracula1_MidLevelGate\][^/]*)/LockableDoor$}) {
    my $g = $1; my $fbx = $byPath{"$path/Quest_Dracula1_MidLevelGate_fbx"};
    out('gate_door', $g, $r, 'scale=' . size($sc), $fbx ? ('hinge=' . join(',', map { num($_) } split / /, $fbx->[1])) : ());
    next;
  }
  if ($path =~ m{/(\[Quest_Dracula1_MidLevelGate\][^/]*)/LockableDoor/AreaBlockingGeo$}) {
    my $g = $1;
    next unless $r->[6] =~ /c65:([^:]+):([^:]+):0/;
    my @c = split / /, $1; my @s = split / /, $2; my @w = split / /, $sc; my @p = split / /, $pos; my @q = split / /, $rot;
    my @lc = map { $c[$_] * $w[$_] } 0 .. 2;
    my ($qx, $qy, $qz, $qw) = @q;
    # rotate lc by q: v + 2w(u x v) + 2 u x (u x v)
    my @u = ($qx, $qy, $qz);
    my @t = (2 * ($u[1] * $lc[2] - $u[2] * $lc[1]), 2 * ($u[2] * $lc[0] - $u[0] * $lc[2]), 2 * ($u[0] * $lc[1] - $u[1] * $lc[0]));
    my @rv = ($lc[0] + $qw * $t[0] + ($u[1] * $t[2] - $u[2] * $t[1]), $lc[1] + $qw * $t[1] + ($u[2] * $t[0] - $u[0] * $t[2]), $lc[2] + $qw * $t[2] + ($u[0] * $t[1] - $u[1] * $t[0]));
    my $wp = join(' ', map { $p[$_] + $rv[$_] } 0 .. 2);
    out('gate_block', $g, [$path, $wp, $rot, $act], 'size=' . join(',', map { num(abs($s[$_] * $w[$_])) } 0 .. 2));
    next;
  }
  # gather pads (exit, mid-level gates, the Grand Hall's four doors): the pad object itself
  if ($leaf =~ /^\[(Temp)?DoorTrigger\]( \(\d+\))?$/) {
    # the pad's own plate (child "Base", 4 x 3 m): its pose and scale are where players gather
    my $b = $byPath{"$path/Base"};
    my @kv = ($encName ne '' ? ('enc=' . enc($encName)) : ());
    if ($b) { push @kv, 'size=' . size($b->[5]); out('door_trigger', $leaf, [$path, $b->[1], $r->[2], $r->[3]], @kv); } else { out('door_trigger', $leaf, $r, @kv); }
    next;
  }
  if ($leaf =~ /^\[Quest_Dracula1_MidLevelGate\]/ && $ei >= 0 && $ei + 1 == $#seg) { out('gate', $leaf, $r); next; }
  if ($leaf =~ /^\[Quest_Dracula1_CollectablesChest\]( \(\d+\))?$/) { out('chest', $leaf, $r); next; }
  if ($leaf =~ /^\[Quest_Dracula1_MirrorCannon\]( \(\d+\))?$/) { out('mirror', $leaf, $r); next; }
  if ($leaf =~ /^\[ActivityDoor\]( \(\d+\))?$/) { out('portal', $leaf, $r); next; }
  # the Dungeon's traps (arrow shooters, axes, fire): their roots, for the hazards
  if ($path =~ m{/Encounters/(Dart Traps|Enable Axe Traps 2|Enable Second Fire Traps|Enable-TrapsEncounter 1)/} && $leaf =~ /^\[?(Quest_Dracula1_)?(Dart|Arrow|Axe|Fire|Flame)[A-Za-z_]*\]?( \(\d+\))?$/i) { out('trap', $leaf, $r, 'enc=' . enc($encName)); next; }
  next if $ei < 0 || $ei + 2 > $#seg || !$isLeaf;
  my @rel = @seg[$ei + 2 .. $#seg];
  my $relS = join('/', @rel);
  next if $relS =~ /(^|\/)(Lights|Collision|Colliders|VFX|Audio|Particles?)(\/|$)|Cinematic|BeamRope|StagePieces|PulsingBeam|_fbx/i;
  my $grp = join('/', @rel[0 .. $#rel - 1]); $grp =~ s/ \(\d+\)//g;
  if ($relS =~ /(^|\/)Triggers\/Overlap Volume( \(\d+\))?$/) { out('enc_trigger', $encName, $r, 'size=' . size($sc)); next; }
  if ($grp =~ /Regions?/i) { out('enemy_region', $leaf, $r, 'enc=' . enc($encName), 'grp=' . enc($grp), 'size=' . size($sc)); next; }
  if ($grp =~ /Respawn/i) { out('respawn', $leaf, $r, 'enc=' . enc($encName)); next; }
  if ($grp =~ /Spawn ?Points?|ExplicitSpawns?|Wave\d*Spawns/i || ($grp eq '' && $leaf =~ /^SpawnPoint/)) { out('enemy_spawner', $leaf, $r, 'enc=' . enc($encName), 'grp=' . enc($grp)); next; }
}
print STDERR "$f: " . join(', ', map { "$_ $n{$_}" } sort keys %n) . "\n";
