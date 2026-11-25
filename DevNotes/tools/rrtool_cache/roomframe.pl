# Converts world_1AF.tsv (Rec Center scene, source frame) to the room frame:
#   X = z + 2.05, Y = y - 0.09, Z = -x - 7.23, yaw' = yaw + 90  (entrance/Dorm at -Z looking +Z, origin = kiosk centre)
use strict; my %inst;
open(my $I, "<", "/c/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-d1a8-4847-ac44-3e6fa88a3591/scratchpad/rrtool/idx/objects.tsv") or die "idx";
while (<$I>) { chomp; my @p = split /\t/; next unless @p >= 4; my $c = $p[2]; $c =~ s/\*$//; $inst{"$c:$p[3]"}++; $inst{"ANY:$p[3]"}++; $inst{"ROOT:$p[3]"}++ if $p[2] eq "GameObject*"; }
close $I;
sub f { sprintf("%.2f", $_[0]) } sub ny { my $a = $_[0] + 90; $a -= 360 while $a > 180; $a += 360 while $a <= -180; sprintf("%.0f", $a) }
my $mode = shift @ARGV; open(my $W, "<", "world_1AF.tsv"); <$W>;
my %seen;
while (<$W>) { chomp; my @c = split /\t/, $_, -1;
  my ($path,$go,$act,$layer,$x,$y,$z,$yaw,$pitch,$roll,$sx,$sy,$sz,$comps,$mesh,$mats,$mn,$mx,$extra) = @c;
  my @seg = split m{/}, $path; my $name = $seg[-1];
  my $X = $z + 2.05; my $Y = $y - 0.09; my $Z = -$x - 7.23;
  my $bb = "";
  if ($mn ne "" && $mn ne "..") { my @a = split /,/, $mn; my @b = split /,/, $mx; $bb = sprintf("X %.2f..%.2f Y %.2f..%.2f Z %.2f..%.2f", $a[2]+2.05, $b[2]+2.05, $a[1]-0.09, $b[1]-0.09, -$b[0]-7.23, -$a[0]-7.23); }
  if ($mode eq "placed") {
    # top-most bracketed prefab instances + named groups
    my $depthBr = grep { /^\[|^\(/ } @seg[0..$#seg-1];
    next unless ($name =~ /^[\[\(]/ && !$depthBr);
    (my $base = $name) =~ s/ \(\d+\)$//; my $bn = $base; $bn =~ s/^\[(.*)\]$/$1/;
    my $m = ($inst{"ROOT:$base"} ? "prefab:$base" : ($inst{"ANY:$base"} ? "name:$base" : ($inst{"ANY:$bn"} ? "name:$bn" : "")));
    my $grp = join("/", @seg[0..$#seg-1]);
    print join("\t", $name, $grp, $act, f($X), f($Y), f($Z), ny($yaw), f($sx).",".f($sy).",".f($sz), $bb, $m), "\n";
  } elsif ($mode eq "render") {
    next unless $mesh ne "";
    print join("\t", $path, $act, f($X), f($Y), f($Z), ny($yaw), f($sx).",".f($sy).",".f($sz), $mesh, $mats, $bb, $extra), "\n";
  } elsif ($mode eq "lights") {
    next unless $extra =~ /light/;
    print join("\t", $path, $act, f($X), f($Y), f($Z), ny($yaw), sprintf("%.0f",$pitch), $extra), "\n";
  } elsif ($mode eq "match") {
    for my $mt (split /\|/, $mats) { $mt =~ s/@.*//; next if $mt eq "" || $seen{"mat:$mt"}++; print "Material\t$mt\t", ($inst{"Material:$mt"} ? "INSTALL" : "-"), "\n"; }
    my $mm = $mesh; $mm =~ s/ col:.*//; $mm =~ s/@.*//; 
    if ($mm ne "" && $mm !~ /^Combined Mesh/ && !$seen{"mesh:$mm"}++) { print "Mesh\t$mm\t", ($inst{"Mesh:$mm"} ? "INSTALL" : "-"), "\n"; }
    if ($mesh =~ / col:(.*)/) { my $cm = $1; $cm =~ s/@.*//; print "Mesh\t$cm\t", ($inst{"Mesh:$cm"} ? "INSTALL" : "-"), "\n" unless $seen{"mesh:$cm"}++; }
    if ($name =~ /^[\[\(]/) { (my $base = $name) =~ s/ \(\d+\)$//; print "Prefab\t$base\t", ($inst{"ROOT:$base"} ? "INSTALL-ROOT" : ($inst{"ANY:$base"} ? "INSTALL-NAME" : "-")), "\n" unless $seen{"pf:$base"}++; }
  }
}
