#!/bin/bash
#
# Migrant test script
#

set -e
set -u

if [ -x "$(command -v realpath)" ]
then
    ROOT_PATH="`dirname \`realpath $0\``"
fi

mono $ROOT_PATH/../packages/NUnit.ConsoleRunner.3.6.1/tools/nunit3-console.exe --labels=all --where="cat != MultiAssemblyTests" $ROOT_PATH/../Tests/bin/Debug/Tests.dll
