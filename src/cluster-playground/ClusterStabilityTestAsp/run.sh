dotnet publish -c Release -r linux-musl-x64 Worker/Worker.csproj
dotnet publish -c Release -r linux-musl-x64 Client/Client.csproj
docker-compose down
docker container prune -f
docker-compose up --build --scale worker=3