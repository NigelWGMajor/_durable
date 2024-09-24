::setlocal
N:
cd N:\_NixNotes\_durable
start cmd /k "azurite"
::cd Template
::dotnet clean N:\_NixNotes\_durable\Template\Template.csproj
::dotnet restore  N:\_NixNotes\_durable\Template\Template.csproj
::dotnet build N:\_NixNotes\_durable\Template\Template.csproj
::func host start --verbose --script-root n:\_NixNotes\_durable\Template
::endlocal