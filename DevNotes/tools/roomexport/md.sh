#!/bin/bash
# md.sh <tsv> : markdown table (type, position, euler rotation, scale, notes) sorted by type
echo "| type | x | y | z | rot x,y,z (deg) | scale | notes |"
echo "| --- | --- | --- | --- | --- | --- | --- |"
tail -n +2 "$1" | sort -t$'\t' -k2,2 -k3,3n | awk -F'\t' '{
  t=$2; if (t ~ /^guid:/) t="UGC " substr(t,6,8);
  n=$12; gsub(/\|/,"/",n); if (length(n)>60) n=substr(n,1,60) "...";
  if ($14>0) n=n (n==""?"":"; ") $14 " shapes";
  if ($13!="") n=n (n==""?"":"; ") $13;
  printf "| %s | %s | %s | %s | %s, %s, %s | %s | %s |\n", t,$3,$4,$5,$6,$7,$8,$9,n }'
