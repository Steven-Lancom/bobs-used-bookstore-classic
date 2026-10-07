using Amazon.Rekognition;
using Amazon.S3;
using BobsBookstoreClassic.Data;
using Bookstore.Data;
using Bookstore.Data.FileServices;
using Bookstore.Data.ImageResizeService;
using Bookstore.Data.ImageValidationServices;
using Bookstore.Data.Repositories;
using Bookstore.Domain;
using Bookstore.Domain.Addresses;
using Bookstore.Domain.Books;
using Bookstore.Domain.Carts;
using Bookstore.Domain.Customers;
using Bookstore.Domain.Offers;
using Bookstore.Domain.Orders;
using Bookstore.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace Bookstore.Web
{
    public static class DependencyInjectionSetup
    {
        public static void ConfigureDependencyInjection(WebApplicationBuilder builder, BookstoreConfiguration config)
        {
            var services = builder.Services;

            // Services
            services.AddScoped<IBookService, BookService>();
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<IReferenceDataService, ReferenceDataService>();
            services.AddScoped<IOfferService, OfferService>();
            services.AddScoped<ICustomerService, CustomerService>();
            services.AddScoped<IAddressService, AddressService>();
            services.AddScoped<IShoppingCartService, ShoppingCartService>();
            services.AddScoped<IImageResizeService, ImageResizeService>();

            // DbContext
            var connectionString = config.GetConnectionString("BookstoreDatabaseConnection");
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            // Repositories
            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<IAddressRepository, AddressRepository>();
            services.AddScoped<IBookRepository, BookRepository>();
            services.AddScoped<IOfferRepository, OfferRepository>();
            services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();

            // Open generic
            services.AddScoped(typeof(IPaginatedList<>), typeof(PaginatedList<>));

            // File service (conditional)
            if (config.GetSetting("Services/FileService") == "aws")
            {
                services.AddScoped<IAmazonS3, AmazonS3Client>();
                services.AddScoped<IFileService, S3FileService>();
            }
            else
            {
                services.AddScoped<IFileService>(sp =>
                {
                    var env = sp.GetRequiredService<IWebHostEnvironment>();
                    var webRootPath = env.WebRootPath ?? Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
                    return new LocalFileService(webRootPath);
                });
            }

            // Image validation service (conditional)
            if (config.GetSetting("Services/ImageValidationService") == "aws")
            {
                services.AddScoped<IAmazonRekognition, AmazonRekognitionClient>();
                services.AddScoped<IImageValidationService, RekognitionImageValidationService>();
            }
            else
            {
                services.AddScoped<IImageValidationService, LocalImageValidationService>();
            }
        }
    }
}
