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
var uri = new Uri(new Uri("http://localhost:9200"));
var pool = new SingleNodeConnectionPool(uri);
var cs = new ConnectionSettings(pool, 
    
        // this is necessary to ensure that the sink can serialize the log event
        // without this, exceptions can not be serialized and will be lost
        // other data may also be lost
        // to prevent data logs, this version of AppFact.SerilogOpenSearchSink will throw an exception,
        // if the serializer is not set up correctly
        AppFact.SerilogOpenSearchSink.Serialization.OpenSearchSerializer.SourceSerializerFactory
    );
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
    uri: "http://localhost:9200", // or submit several see method 2 with SniffingConnectionPool
    basicAuthUser: "username",
    basicAuthPassword: "password",
    index: "logs", // optional, default is "logs"
    maxBatchSize: 1000, // optional and nullable, default is 1000
    tickInSeconds: 1.0, // optional double, default is 1.0
    restrictedToMinimumLevel: LevelAlias.Minimum, // optional enumerator, default is LevelAlias.Minimum
    levelSwitch: null, // optional Serilog.Core.LoggingLevelSwitch, default is null
    bypassSsl: false, // .NET will throw an exception when a server certificate is issued by an untrasted authority. To bypass the SSL certificate check set the value to true, default is false
    suppressThrowOnFailedInit: false // optional bool, default is false.
        // ^ should the sink suppress throwing an exception if the ping to the OpenSearch cluster fails on startup. this has no effect if the ping fails after the sink has started
);

// method 2 with SniffingConnectionPool
builder.WriteTo.OpenSearch(
    connectionStrings: new Uri[]
    {
        new Uri("http://localhost:9200"),
        new Uri("http://localhost:9201"),
        new Uri("http://localhost:9202")
    },
    // the rest of parameters
);


// finally
// build logger
var logger = builder.CreateLogger();
```