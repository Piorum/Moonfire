#Moonfire
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /App

COPY AzureAllocator/. ./AzureAllocator/
COPY FuncExt/. ./FuncExt/
COPY Moonfire/. ./Moonfire/

WORKDIR /App/Moonfire

RUN dotnet restore
RUN dotnet publish -o out

FROM mcr.microsoft.com/dotnet/runtime:9.0
RUN apt-get update \
 && apt-get install -y --no-install-recommends openssh-client \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /App

COPY Config/. ./Config/
COPY quotas.json ./

COPY --from=build /App/Moonfire/out .
ENTRYPOINT [ "dotnet", "Moonfire.dll" ]