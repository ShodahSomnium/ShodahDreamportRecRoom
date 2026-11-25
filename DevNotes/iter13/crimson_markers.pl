#!/usr/bin/perl
# crimson_markers.pl - extra G markers for DN/refdata/apk/Crimson_<Room>.rrsk (iteration 13, crimson agent): the Curse of
# the Crimson Cauldron's Android levels 50-60 (Quest_Goblin2_*). A copy of DN/iter8/jumbotron_markers.pl for the Goblin2
# naming (reads the same full GameObject dump of DN/iter8/jumbotron_tools/make_tools.sh's apks `find <work> levelN - .`):
#   - encounters live under Systems/Encounters (not EncountersV2): enemy spawn points (enemy_spawner, enc= grp=), spawn and
#     patrol regions (enemy_region, size = world scale) and the "Overlap Volume" triggers (enc_trigger, size = world scale);
#     the boss's WitchPatrol points (witch_point) and WitchSpawnPoint (witch_spawn); cinematic subtrees are left out;
#   - explosive barrels (PointsDropper on a *Barrel*Explosive* / Explosive*Barrel* root: barrel);
#   - ladders (Rec Room's TeleportationPortal pair: ladder = the volume, size = collider size x world scale; ladder_top =
#     the destination; lad= the ladder's own path so the two pair up);
#   - steam traps ([Quest_SteamTrap]: steam), the Bog's floats ([FloatingPalletPlatform]: float), the pit kill zone
#     (PitKillzone, size = world scale: killzone), gift-box spawns (giftbox), the doors (LockableDoor: door) and
#     portals/buttons as in the Jumbotron script.
# Only names and numbers are written (placement data derived from the reference level; nothing is copied).
#   perl crimson_markers.pl levelN.txt >> Crimson_<Room>.rrsk
use strict; use warnings;
my $f = shift or die "usage: crimson_markers.pl <dump.txt>\n";
open(my $h, '<', $f) or die "$f: $!\n";
my (@rows, %isParent);
while (my $l = <$h>) {
  chomp $l; $l =~ s/\r$//;
  my @c = split / \| /, $l, -1;
  next if @c < 5 || $c[1] !~ /^-?[0-9]/;
  my ($path, $pos, $rot, $act, $scr, $sc, $comps, $cols) = @c;
  $sc = defined $sc ? $sc : 's=1 1 1'; $sc =~ s/^s=//;
  push @rows, [$path, $pos, $rot, $act, $scr, $sc, defined $cols ? $cols : ''];
  my @seg = split m{/}, $path;
  for my $i (1 .. $#seg) { $isParent{join('/', @seg[0 .. $i - 1])} = 1; }
}
close $h;
sub enc { my $s = shift; $s =~ s/%/%25/g; $s =~ s/ /%20/g; return $s; }
sub num { my $v = shift; $v = sprintf('%.4f', $v); $v =~ s/0+$//; $v =~ s/\.$//; $v = '0' if $v eq '-0' || $v eq ''; return $v; }
sub trs { my ($p, $r) = @_; return join(' ', map { num($_) } (split(/ /, $p), split(/ /, $r))); }
sub size { my $s = shift; return join(',', map { num(abs($_)) } split / /, $s); }
# a trigger box collider's world size: its size (c65t:<centre>:<size>:..) x the object's world scale
sub colsize {
  my ($cols, $sc) = @_;
  return undef unless $cols =~ /c65t?:[^:]*:([-0-9.eE]+) ([-0-9.eE]+) ([-0-9.eE]+):/;
  my @s = ($1, $2, $3); my @w = split / /, $sc;
  return join(',', map { num(abs($s[$_] * ($w[$_] // 1))) } 0 .. 2);
}
my %seen; my $n = 0;
sub out {
  my ($kind, $name, $row, @kv) = @_;
  my $key = "$kind|$row->[0]|$row->[1]";
  return if $seen{$key}++;
  my $act = $row->[3] =~ /True/ ? 1 : 0;
  my $p = $row->[0]; $p = substr($p, -120) if length $p > 120;
  print join(' ', 'G', $kind, enc($name), trs($row->[1], $row->[2]), @kv, "active=$act", 'path=' . enc($p)), "\n";
  $n++;
}
for my $r (@rows) {
  my ($path, $pos, $rot, $act, $scr, $sc, $cols) = @$r;
  my @seg = split m{/}, $path;
  my $leaf = $seg[-1];
  my $isLeaf = !$isParent{$path};
  my $base = $leaf; $base =~ s/ \(\d+\)$//; $base =~ s/ +$//;
  if ($scr =~ /\bLockableDoor\b/) { out('door', $leaf, $r); next; }
  if ($scr =~ /\bDoorUnlockTrigger\b/) { out('door_trigger', $leaf, $r); next; }
  if ($scr =~ /QuestPreGameRoom/) { out('pregame', $leaf, $r); next; }
  if ($scr =~ /\bSimplePhysicalButton\b/) { out('button', $leaf, $r); next; }
  if ($scr =~ /PointsDropper/ && $base =~ /Barrel.*Explosive|Explosive.*Barrel/i) { out('barrel', $base, $r); next; }
  if ($base eq '[Quest_SteamTrap]') { out('steam', $base, $r); next; }
  if ($base eq '[FloatingPalletPlatform]') { out('float', $base, $r); next; }
  if ($leaf eq 'PitKillzone') { out('killzone', $leaf, $r, 'size=' . size($sc)); next; }
  if ($leaf =~ /GiftBoxSpawnPoint$/) { out('giftbox', $leaf, $r); next; }
  # ladders: <ladder>/TeleportVolume (TeleportationPortal, a trigger box) and <ladder>/TeleportDestination
  if (@seg >= 2 && ($leaf eq 'TeleportVolume' || $leaf eq 'TeleportDestination')) {
    my $lad = join('/', @seg[0 .. $#seg - 1]); $lad = substr($lad, -80) if length $lad > 80;
    if ($leaf eq 'TeleportVolume') { my $cs = colsize($cols, $sc); out('ladder', $seg[-2], $r, 'lad=' . enc($lad), defined $cs ? "size=$cs" : ()); }
    else { out('ladder_top', $seg[-2], $r, 'lad=' . enc($lad)); }
    next;
  }
  next if $scr =~ /\bTeleportationPortal\b/;
  # encounters: Encounters / EncountersV2 / <encounter> / ...
  my $ei = -1;
  for my $i (0 .. $#seg) { if ($seg[$i] =~ /^(Encounters|EncountersV2)$/) { $ei = $i; last; } }
  next if $ei < 0 || $ei + 1 > $#seg || !$isLeaf;
  my $encName = $seg[$ei + 1];
  my @rel = @seg[$ei + 2 .. $#seg];
  next if !@rel;
  my $relS = join('/', @rel);
  # the encounter's trigger volumes first: the exclusions below match "Rig" inside "Triggers"
  if ($relS =~ /(^|\/)Triggers?\/Overlap Volume( \(\d+\))?$/) { out('enc_trigger', $encName, $r, 'size=' . size($sc)); next; }
  next if $relS =~ /Cinematic|(^|\/)(Lights|Collision|Colliders|QuestLightShafts|VO|SFX|Audio)(\/|$)|StageLight|_fbx|Particle|VFX|Rig|Jnt|VisualRoot|Scoreboard/i;
  next if $encName =~ /Scoreboard|Leaderboard/;
  my $grp = join('/', @rel[0 .. $#rel - 1]); $grp =~ s/ \(\d+\)//g;
  if ($relS =~ /(^|\/)Triggers?\/Overlap Volume( \(\d+\))?$/) { out('enc_trigger', $encName, $r, 'size=' . size($sc)); next; }
  if ($leaf =~ /^(WitchPoint|InitialWitchPoint|FinalWitchPoint|RetreatPoint)( \(\d+\))?$/) { out('witch_point', $leaf, $r, 'enc=' . enc($encName)); next; }
  if ($leaf =~ /^WitchSpawnPoint/) { out('witch_spawn', $leaf, $r, 'enc=' . enc($encName)); next; }
  if ($leaf =~ /Region|Patrol/i || $grp =~ /(Spawn|Patrol) Regions?$/i || $grp =~ /Regions$/) { out('enemy_region', $leaf, $r, 'enc=' . enc($encName), 'grp=' . enc($grp), 'size=' . size($sc)); next; }
  next if $sc ne '1 1 1' && $sc !~ /^1 1 1$/ && $grp eq '';
  out('enemy_spawner', $leaf, $r, 'enc=' . enc($encName), 'grp=' . enc($grp));
}
print STDERR "$f: $n extra markers\n";
