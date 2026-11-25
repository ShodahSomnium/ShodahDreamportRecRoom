#!/usr/bin/perl
# dg_cover.pl - iteration 15 Disc Golf: which 2 m cells of a course have something solid under them (a vertical ray
# hits a C box / capsule, or a collider=1 H grid covers it), to compare a level before and after thinning.
#   perl dg_cover.pl <rrsk> x0 z0 x1 z1 [cell=2] [map=out.txt]
use strict; use warnings;
my ($f, $x0, $z0, $x1, $z1, @o) = @ARGV; my %o = (cell => 2); for (@o) { my ($k, $v) = split /=/, $_, 2; $o{$k} = $v; }
my $c = $o{cell}; my $nx = int(($x1 - $x0) / $c); my $nz = int(($z1 - $z0) / $c); my @g;
sub rot { my ($qx,$qy,$qz,$qw,$x,$y,$z)=@_; my ($tx,$ty,$tz)=(2*($qy*$z-$qz*$y),2*($qz*$x-$qx*$z),2*($qx*$y-$qy*$x));
  return ($x+$qw*$tx+($qy*$tz-$qz*$ty), $y+$qw*$ty+($qz*$tx-$qx*$tz), $z+$qw*$tz+($qx*$ty-$qy*$tx)); }
open(my $h, '<', $f) or die; my %grid;
while (<$h>) {
  my @a = split; next unless @a;
  if ($a[0] eq 'C' && $a[1] eq 'box') {
    my ($cx,$cy,$cz,$qx,$qy,$qz,$qw,$sx,$sy,$sz) = @a[2..11]; my @hx = ($sx/2,$sy/2,$sz/2);
    my $r = sqrt($hx[0]**2+$hx[1]**2+$hx[2]**2);
    # inverse rotation of the down ray
    my @d = rot(-$qx,-$qy,-$qz,$qw,0,-1,0);
    for my $i (int(($cx-$r-$x0)/$c) .. int(($cx+$r-$x0)/$c)) { next if $i<0||$i>=$nx;
      for my $j (int(($cz-$r-$z0)/$c) .. int(($cz+$r-$z0)/$c)) { next if $j<0||$j>=$nz; next if $g[$i][$j];
        my ($px,$pz) = ($x0+($i+0.5)*$c, $z0+($j+0.5)*$c);
        my @o = rot(-$qx,-$qy,-$qz,$qw,$px-$cx,1000,$pz-$cz);
        my ($tmin,$tmax)=(-1e9,1e9); my $ok=1;
        for my $k (0..2) { if (abs($d[$k])<1e-9) { if (abs($o[$k])>$hx[$k]) {$ok=0;last} next }
          my $t1=(-$hx[$k]-$o[$k])/$d[$k]; my $t2=($hx[$k]-$o[$k])/$d[$k]; ($t1,$t2)=($t2,$t1) if $t1>$t2;
          $tmin=$t1 if $t1>$tmin; $tmax=$t2 if $t2<$tmax; if ($tmin>$tmax) {$ok=0;last} }
        $g[$i][$j]=1 if $ok; } } }
  elsif ($a[0] eq 'H' && $a[1] eq 'grid' && /collider=1/) {
    my ($ox,$oz,$dx,$dz,$gx,$gz)=($a[3],$a[5],$a[6],$a[7],$a[8],$a[9]);
    for my $i (0..$nx-1) { for my $j (0..$nz-1) { my ($px,$pz)=($x0+($i+0.5)*$c,$z0+($j+0.5)*$c);
      $g[$i][$j]=1 if $px>=$ox && $px<=$ox+$dx*($gx-1) && $pz>=$oz && $pz<=$oz+$dz*($gz-1); } } }
}
my $n=0; for my $i (0..$nx-1) { for my $j (0..$nz-1) { $n++ if $g[$i][$j] } }
printf "%s: %d of %d cells (%.1f %%) have something solid under them\n", $f, $n, $nx*$nz, 100*$n/($nx*$nz);
if ($o{map}) { open(my $m,'>',$o{map}); for (my $j=$nz-1;$j>=0;$j-=2) { my $s=''; for (my $i=0;$i<$nx;$i+=2) { $s .= $g[$i][$j] ? '#' : '.' } print $m "$s\n" } close $m }
