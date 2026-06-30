FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Quan4CulinaryTourism.Api.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 python3-venv ca-certificates \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
RUN python3 -m venv /opt/ttsenv \
    && /opt/ttsenv/bin/pip install --no-cache-dir -r /app/tools/requirements-tts.txt \
    && ln -sf /opt/ttsenv/bin/python /usr/local/bin/python
ENV ASPNETCORE_URLS=http://+:8080
ENV TextToSpeechSettings__PythonCommand=/opt/ttsenv/bin/python
EXPOSE 8080
ENTRYPOINT ["dotnet", "Quan4CulinaryTourism.Api.dll"]
