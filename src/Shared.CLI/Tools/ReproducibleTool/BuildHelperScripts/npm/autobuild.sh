#!/bin/bash

PREBUILD_SCRIPT="$1"
BUILD_SCRIPT="$2"
POSTBUILD_SCRIPT="$3"

if [ ! -z "$PREBUILD_SCRIPT" -a -f "/build-helpers/$PREBUILD_SCRIPT" ]; then
	echo "Executing pre-build script: [$PREBUILD_SCRIPT]"
	source "/build-helpers/$PREBUILD_SCRIPT"
else
	echo "No custom pre-build script found."
fi

if [ ! -z "$BUILD_SCRIPT" -a -f "/build-helpers/$BUILD_SCRIPT" ]; then
	echo "Executing build script: [$BUILD_SCRIPT]"
	source "/build-helpers/$BUILD_SCRIPT"
else
	echo "No custom build script found. Using auto-builder."
	echo "Executing 'npm install'"
	npm install

	echo "Executing npm scripts"
	# Note, we expect most of these to fail gracefully
	npm run preprepare
	npm run prepare
	npm run postprepare
	
	npm run prepack
	npm run pack
	npm run postpack
	
	npm run build
	npm pack
	npm run prepublish
fi

if [ ! -z "$POSTBUILD_SCRIPT" -a -f "/build-helpers/$POSTBUILD_SCRIPT" ]; then
	echo "Executing post-build script: [$POSTBUILD_SCRIPT]"
	source "/build-helpers/$POSTBUILD_SCRIPT"
else
	echo "No custom post-build script found. Using default packer."
	echo "Running 'npm pack'"
	npm pack --json > /build-output/output.json
	cp *.tgz /build-output/output.archive
fi

echo "Autobuild complete."
