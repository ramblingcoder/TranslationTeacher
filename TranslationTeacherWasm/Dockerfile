FROM nginx AS base
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["TranslationTeacherWasm/TranslationTeacherWasm.csproj", "TranslationTeacherWasm/"]
RUN dotnet restore "TranslationTeacherWasm/TranslationTeacherWasm.csproj"
COPY . .
WORKDIR "/src/TranslationTeacherWasm"
RUN dotnet build "./TranslationTeacherWasm.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./TranslationTeacherWasm.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /usr/share/nginx/html
COPY --from=publish /app/publish/wwwroot .
COPY TranslationTeacherWasm/nginx.conf /etc/nginx/nginx.conf