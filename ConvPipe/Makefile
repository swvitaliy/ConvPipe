SHELL=/bin/bash
PACKAGE_CSPROJ=$(shell ls *.csproj)
PACKAGE_NAME=${PACKAGE_KEY}.${PACKAGE_VERSION}

PACKAGE_DIR=../.packages

APIKEY=$(shell cat ../nuget-apikey.txt)

push:
	dotnet publish -c Release && \
		dotnet nuget push -s 'https://api.nuget.org/v3/index.json' -k "${APIKEY}" "bin/Release/ConvPipe.0.0.7.nupkg"

