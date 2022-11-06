publish:
	dotnet publish -c Release -r linux-x64 \
		-p:PublishReadyToRun=true -p:PublishTrimmed=true -p:PublishSingleFile=true -p:SelfContained=true

publish_min:
	dotnet publish -r linux-x64 -c Release \
		-p:PublishReadyToRun=true -p:PublishTrimmed=false -p:PublishSingleFile=true -p:SelfContained=false
