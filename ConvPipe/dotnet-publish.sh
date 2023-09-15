#!/bin/bash

parse_version_csproj() {
    CSPROJ=${1}
	VERSION=$(grep VersionPrefix "${CSPROJ}" | grep -oP '([0-9]\.?)+')
	# VERSION_SUFFIX=$(grep VersionSuffix "${CSPROJ}" | grep -oP '(?<=>).+(?=<)') || true
	# echo "${VERSION}-${VERSION_SUFFIX}"
    echo "${VERSION}"
}

PACKAGE_VERSION=$(parse_version_csproj ConvPipe.csproj)

APIKEY=$(cat ../nuget-apikey.txt)

dotnet publish -c Release && \
  dotnet nuget push -s 'https://api.nuget.org/v3/index.json' -k "${APIKEY}" "bin/Release/ConvPipe.${PACKAGE_VERSION}.nupkg"
