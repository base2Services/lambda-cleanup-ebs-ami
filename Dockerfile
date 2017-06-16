FROM microsoft/dotnet:latest

RUN mkdir /project
COPY . /project
WORKDIR /project
RUN dotnet restore
RUN dotnet build

CMD ["dotnet","run"]