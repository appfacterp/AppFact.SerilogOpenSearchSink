# AppFact.SerilogOpenSearchSink

## What is it?

A [Serilog](https://serilog.net/) sink that writes logs to an [OpenSearch](https://opensearch.org/) cluster.

## How to use it?

### Install

Add the `AppFact.SerilogOpenSearchSink` NuGet package to your project.

### Configure

As per usual, the sink is configured via `WriteTo.OpenSearch()`.

```csharp
var builder = new LoggerConfiguration();

// mathod 1
// configure via IConnectionSettingsValues provided by OpenSeaerch.
// provides the most flexibility but won't work with with Serilog.Settings.Configuration
var cs = new ConnectionSettings(new Uri("http://localhost:9200"));
// configure cs as needed, e.g.
cs.DefaultIndex("logs");
cs.BasicAuthentication("username", "password");
// register sink
builder.WriteTo.OpenSearch(cs);
// also supports optional parameters options, restrictedToMinimumLevel, and levelSwitch
builder.WriteTo.OpenSearch(cs, options: new OpenSearchSinkOptions{...}, restrictedToMinimumLevel: LevelAlias.Minimum, levelSwitch: new Serilog.Core.LoggingLevelSwitch());

// method 2
// or configure without IConnectionSettingsValues using basic auth
// provides less flexibility but works with Serilog.Settings.Configuration
builder.WriteTo.OpenSearch(
    uri: "http://localhost:9200",
    basicAuthUser: "username",
    basicAuthPassword: "password",
    index: "logs", // optional, default is "logs"
    maxBatchSize: 1000, // optional and nullable, default is 1000
    tickInSeconds: 1.0, // optional double, default is 1.0
    restrictedToMinimumLevel: LevelAlias.Minimum, // optional enumerator, default is LevelAlias.Minimum
    levelSwitch: null, // optional Serilog.Core.LoggingLevelSwitch, default is null
    bypassSsl: false // .NET will throw an exception when a server certificate is issued by an untrasted authority. To bypass the SSL certificate check set the value to true, default is false
);


// finally
// build logger
var logger = builder.CreateLogger();
```
