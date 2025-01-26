using Amazon.DynamoDBv2;
using Amazon.S3;
using FileStorage.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(options =>
{
    options.Filters.Add(new RequestSizeLimitAttribute(100_000_000)); // Set limit to 100 MB
});

builder.Services.AddHealthChecks();

// service registration -> automatically create all instances of dependencies
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddSingleton<IAmazonS3>(serviceProvider =>
{
    var config = builder.Configuration.GetSection("S3Bucket:Config");
    var amazonS3Config = config.Get<AmazonS3Config>();
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

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();