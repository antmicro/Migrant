#!/bin/bash
#
# Migrant build script
#

set -e
set -u

TARGET=Build
CLEAN=false
TESTS=false
SOLUTION=Migrant

function print_help() {
    echo -e "Usage: $0 [-c] [-t]\n\n-c    clean\n-t    build tests suite"
}

if [ -x "$(command -v realpath)" ]
then
    ROOT_PATH="`dirname \`realpath $0\``"
fi

while getopts ":cth" opt
do
    case $opt in
        c)
            TARGET=Clean
            CLEAN=true
            ;;
        t)
            TESTS=true
            SOLUTION=MigrantWithTests
            ;;
        h)
            print_help
            exit 0
            ;;
        \?)
            echo -e "Invalid option: -$OPTARG\n" >&2
            print_help
            exit 1
            ;;
    esac
done

if $CLEAN
then
    rm -rf $ROOT_PATH/../packages
elif $TESTS
then
    nuget install -OutputDirectory $ROOT_PATH/../packages $ROOT_PATH/../PerformanceTester/packages.config
    nuget install -OutputDirectory $ROOT_PATH/../packages $ROOT_PATH/../Tests/packages.config
fi

xbuild $ROOT_PATH/../$SOLUTION.sln /t:$TARGET
