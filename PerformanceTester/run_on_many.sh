#!/bin/bash

# This script is provided to gather performance results on the range of commits

set -e
set -u

if [ $# -ne 2 ]
then
  echo "Usage: $0 oldest_rev newest_rev"
  echo -e "\t oldest_rev - oldest git revision to do performance tests on"
  echo -e "\t newest_rev - newest git revision to do performance tests on"
  exit 1
fi

rev_list=`git rev-list $1..$2`
for rev in $rev_list
do
  git checkout $rev -- ../Migrant
  xbuild /property:Configuration=Release
  mono bin/Release/PerformanceTester.exe -o results.csv -i $rev
done
