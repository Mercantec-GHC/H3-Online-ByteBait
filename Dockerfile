# One image with the API and the Blazor client. Both Dokploy apps (Test-ByteBait and Main-ByteBait) build from it.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, so Docker can reuse the restore layer until a package changes
COPY Backend/Directory.Build.props Backend/
COPY Backend/ByteBait.Api/ByteBait.Api.csproj Backend/ByteBait.Api/
COPY Backend/ByteBait.Data/ByteBait.Data.csproj Backend/ByteBait.Data/
COPY Frontend/ByteBait.Client/ByteBait.Client.csproj Frontend/ByteBait.Client/
RUN dotnet restore Backend/ByteBait.Api/ByteBait.Api.csproj

COPY Backend/ByteBait.Api/ Backend/ByteBait.Api/
COPY Backend/ByteBait.Data/ Backend/ByteBait.Data/
COPY Frontend/ByteBait.Client/ Frontend/ByteBait.Client/

# The API references the client, so the Blazor files end up in wwwroot of the same output
RUN dotnet publish Backend/ByteBait.Api/ByteBait.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# The aspnet image listens on port 8080 and comes with a non-root user called app
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "ByteBait.Api.dll"]
