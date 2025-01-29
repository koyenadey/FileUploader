using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Transfer;
using FileStorage.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddControllers(options =>
{
    options.Filters.Add(new RequestSizeLimitAttribute(100_000_000)); // Set limit to 100 MB
});

builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
    });

builder.Services.AddHealthChecks();

// service registration -> automatically create all instances of dependencies
builder.Services.AddScoped<IFileStorageService, FileStorageService>();


builder.Services.AddSingleton<IAmazonS3>(serviceProvider =>
{
    var amazonS3Config = new AmazonS3Config
    {
        RegionEndpoint = Amazon.RegionEndpoint.USEast1,
        ServiceURL = builder.Configuration["S3Bucket:Config:ServiceUrl"],
        ForcePathStyle = builder.Configuration.GetValue<bool>("S3Bucket:Config:ForcePathStyle"),
        MaxErrorRetry = builder.Configuration.GetValue<int>("S3Bucket:Config:MaxErrorRetry"),
        Timeout = TimeSpan.FromSeconds(30),
        RetryMode = RequestRetryMode.Standard
    };
    return new AmazonS3Client(amazonS3Config);
});

builder.Services.AddSingleton<IAmazonDynamoDB>(serviceProvider =>
{
    var dynamoDbConfig = new AmazonDynamoDBConfig
    {
        ServiceURL = builder.Configuration["DynamoDb:ServiceURL"]
    };
    return new AmazonDynamoDBClient(dynamoDbConfig);
});

builder.Services.AddSingleton<ITransferUtility>(sp =>
{
    var s3Client = sp.GetRequiredService<IAmazonS3>();
    return new TransferUtility(s3Client);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();