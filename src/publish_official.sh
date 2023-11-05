#!/usr/bin/env bash
#
# must be run in src/ directory
set -e

VERSION="$1"
BASEDIR="$(dirname "$0")"

if [ ! -n "$VERSION" ]
then 
    echo "Must pass a version number as the initial argument, ie:  publish_official 1.2.0"
    exit -1
fi

# path variables
ROOTDIR="$BASEDIR/.."
PUBLISHDIR="$ROOTDIR/publish"
BUILDDIR="$ROOTDIR/build"
TEMPDIR="$BUILDDIR/publish-temp"
ARTIFACTSDIR="$BUILDDIR/artifacts/electron-winstaller/vendor"
VENDORDIR="$ROOTDIR/vendor"
OUTPUTPATH="../publish/Squirrel.Windows-$VERSION.7z"

# cleanup directories and create new ones ready for processing
rm -rf $PUBLISHDIR
mkdir -p $PUBLISHDIR
mkdir -p $TEMPDIR

# copy all files necessary to build/publish-temp/
cp "$ARTIFACTSDIR/"* $TEMPDIR
cp "$VENDORDIR/wix/"* $TEMPDIR
cp "$ROOTDIR/install-spinner.gif" "$TEMPDIR/install-spinner.gif"

"$VENDORDIR/7zip/7z.exe" a -mx=9 -mfb=64 "$OUTPUTPATH" "$TEMPDIR/."

CHECKSUM=$(shasum -a 512 "$OUTPUTPATH" | xxd -r -p |  base64 -w 0)

printf "Checksum is\n\n"
printf "$CHECKSUM"
printf "\n\n"
printf "File to publish is\n\n"
printf `realpath $OUTPUTPATH`
printf "\n\n"

rm -rf $TEMPDIR