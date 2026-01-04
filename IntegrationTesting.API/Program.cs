
namespace IntegrationTesting.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Configure AWS S3 client. Resolve IConfiguration at runtime so test
            // overrides added via ConfigureAppConfiguration are respected.
            builder.Services.AddScoped<Amazon.S3.IAmazonS3>(provider =>
            {
                var configuration = provider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
                var awsOptions = configuration.GetSection("AWS");
                var s3ServiceUrl = awsOptions.GetValue<string>("S3ServiceUrl");
                var awsAccessKeyId = awsOptions.GetValue<string>("AccessKeyId");
                var awsSecretAccessKey = awsOptions.GetValue<string>("SecretAccessKey");
                var awsRegion = awsOptions.GetValue<string>("Region");

                if (!string.IsNullOrEmpty(s3ServiceUrl))
                {
                    var s3Config = new Amazon.S3.AmazonS3Config
                    {
                        ServiceURL = s3ServiceUrl,
                        UseHttp = !s3ServiceUrl.StartsWith("https"),
                        ForcePathStyle = true
                    };

                    var credentials = new Amazon.Runtime.BasicAWSCredentials(
                        awsAccessKeyId ?? "test",
                        awsSecretAccessKey ?? "test"
                    );

                    return new Amazon.S3.AmazonS3Client(credentials, s3Config);
                }

                return new Amazon.S3.AmazonS3Client(
                    Amazon.RegionEndpoint.GetBySystemName(awsRegion ?? "us-east-1")
                );
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
