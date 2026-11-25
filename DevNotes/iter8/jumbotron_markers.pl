#!/usr/bin/perl
# jumbotron_markers.pl - extra G markers for DN/refdata/apk/Jumbotron_<Room>.rrsk (iteration 8, jumbotron agent).
#
# apkscene's converter keeps only the markers it knows (spawns, weapons, scoreboards, trigger colliders). The Rise of
# Jumbotron's levels drive their fights from plain Transforms under EncountersV2 (no script the build can read): enemy
# spawn points, spawn regions and "Overlap Volume" triggers whose world SCALE is the volume's size (measured: Encounter1
# of Battlefield1 is 26.4 x 19.7 x 18.7 m, its Spawn Region 13.9 x 1 x 2.1 m). This script reads a full GameObject dump
# of one level and prints those as RRSK G records, plus the exit doors, door triggers, explosive barrels, terminals
# (credit-dropping computers), activity portals, the pre-game room, the encounter button, cinematic points and the boss
# spawn. Only names and numbers are written (placement data derived from the reference level; nothing is copied).
#
# The dump comes from a scratch build of DN/tools/apkscene whose `find` accepts script "-" (any GameObject) and prints
# "path | pos | rot | act= | scripts | s=<world scale> | comps=<n> | colliders" (two edits to Conv.cs Find.Run: the
# script test `scriptRe == "-" ||`, the 400-line cap raised, and the extra columns). Usage:
#   bash run.sh find <work> levelN - . > levelN.txt ; perl jumbotron_markers.pl levelN.txt >> Jumbotron_<Room>.rrsk
use strict; use warnings;
my $f = shift or die "usage: jumbotron_markers.pl <dump.txt>\n";
open(my $h, '<', $f) or die "$f: $!\n";
my (@rows, %isParent);
while (my $l = <$h>) {
  chomp $l; $l =~ s/\r$//;
  my @c = split / \| /, $l, -1;
  next if @c < 5 || $c[1] !~ /^-?[0-9]/;
  my ($path, $pos, $rot, $act, $scr, $sc) = @c;
  $sc = defined $sc ? $sc : 's=1 1 1'; $sc =~ s/^s=//;
  push @rows, [$path, $pos, $rot, $act, $scr, $sc];
  my @seg = split m{/}, $path;
  for my $i (1 .. $#seg) { $isParent{join('/', @seg[0 .. $i - 1])} = 1; }
}
close $h;
sub enc { my $s = shift; $s =~ s/%/%25/g; $s =~ s/ /%20/g; return $s; }
sub num { my $v = shift; $v = sprintf('%.4f', $v); $v =~ s/0+$//; $v =~ s/\.$//; $v = '0' if $v eq '-0' || $v eq ''; return $v; }
sub trs { my ($p, $r) = @_; return join(' ', map { num($_) } (split(/ /, $p), split(/ /, $r))); }
sub size { my $s = shift; return join(',', map { num(abs($_)) } split / /, $s); }
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
  my ($path, $pos, $rot, $act, $scr, $sc) = @$r;
  my @seg = split m{/}, $path;
  my $leaf = $seg[-1];
  my $isLeaf = !$isParent{$path};
  my $base = $leaf; $base =~ s/ \(\d+\)$//;
  if ($scr =~ /\bLockableDoor\b/) { out('door', $leaf, $r); next; }
  if ($scr =~ /\bDoorUnlockTrigger\b/) { out('door_trigger', $leaf, $r); next; }
  if ($scr =~ /\bTeleportationPortal\b/) { out('portal', $leaf, $r); next; }
  if ($scr =~ /QuestPreGameRoom/) { out('pregame', $leaf, $r); next; }
  if ($scr =~ /\bSimplePhysicalButton\b/) { out('button', $leaf, $r); next; }
  if ($base eq '[SciFi_Explosive_Barrel]' && $scr =~ /PointsDropper/) { out('barrel', $base, $r); next; }
  if ($base =~ /^\[Scifi_Terminal[A-Za-z_]*\]/ && $scr =~ /PointsDropper/) { (my $b = $base) =~ s/ +$//; out('terminal', $b, $r); next; }
  if ($leaf =~ /ProjectorScreen$/) { out('cinematic', $leaf, $r, 'size=' . size($sc)); next; }
  # encounters: EncountersV2 / CinematicEncounters(V2) / <encounter> / ...
  my $ei = -1;
  for my $i (0 .. $#seg) { if ($seg[$i] =~ /^(EncountersV2|CinematicEncounters|CinematicEncountersV2)$/) { $ei = $i; last; } }
  next if $ei < 0 || $ei + 1 > $#seg || !$isLeaf;
  my $encName = $seg[$ei + 1];
  my @rel = @seg[$ei + 2 .. $#seg];
  next if !@rel;
  my $relS = join('/', @rel);
  next if $relS =~ /(^|\/)(Lights|Collision|Colliders|QuestLightShafts)(\/|$)|StageLight|_fbx|Particle|VFX|LightArch|Spotlight|Bounce|Hotspot|HotSpot/i;
  my $grp = join('/', @rel[0 .. $#rel - 1]); $grp =~ s/ \(\d+\)//g;
  my $cine = $seg[$ei] =~ /^Cinematic/ ? 1 : 0;
  if ($relS =~ /(^|\/)Triggers\/Overlap Volume( \(\d+\))?$/) { out($cine ? 'cinematic_trigger' : 'enc_trigger', $encName, $r, 'size=' . size($sc)); next; }
  if ($cine || $relS =~ /(^|\/)(VO|SFX)|Audio|Cinematic|VideoPlayer|Screen|Laughter|Buzzer|LetTheGames|Contestants|MadeItAll/) { out('cinematic', $leaf, $r, 'enc=' . enc($encName), 'grp=' . enc($grp)); next; }
  if ($leaf =~ /Region/i || $grp =~ /Spawn Regions?$/i || $grp =~ /Regions$/) { out('enemy_region', $leaf, $r, 'enc=' . enc($encName), 'grp=' . enc($grp), 'size=' . size($sc)); next; }
  if ($leaf =~ /Door$|^SpawnDoor|Exploders$/ && $sc eq '1 1 1' && $grp eq '') { next; }
  out('enemy_spawner', $leaf, $r, 'enc=' . enc($encName), 'grp=' . enc($grp));
}
print STDERR "$f: $n extra markers\n";
