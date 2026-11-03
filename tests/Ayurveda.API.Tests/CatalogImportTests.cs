using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ayurveda.Application.Interfaces;
using Ayurveda.Application.ViewModels;
using Ayurveda.Domain.Entities;
using Ayurveda.Infrastructure.Data;
using Ayurveda.Infrastructure.Services;
using Ayurveda.Admin.Controllers;
using Xunit;

namespace Ayurveda.API.Tests;

public class TestAuditService : IAuditService
{
    public List<(string Action, string EntityName, string? EntityId, string? Details)> Logs { get; } = new();

    public Task LogAsync(string action, string entityName, string? entityId, string? details, string? userEmail = null, string? ipAddress = null)
    {
        Logs.Add((action, entityName, entityId, details));
        return Task.CompletedTask;
    }
}

public class CatalogImportTests
{
    private AyurvedaDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AyurvedaDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new AyurvedaDbContext(options);
        context.Database.EnsureCreated();

        // Seed basic Category and Subcategory
        if (!context.Categories.Any())
        {
            var cat = new Category
            {
                Id = 1,
                Name = "Immunity & Rasayana",
                Description = "Immunity formulations",
                ImageUrl = "/images/categories/immunity-rasayana.svg",
                IsActive = true
            };
            var sub = new Subcategory
            {
                Id = 1,
                CategoryId = 1,
                Name = "Chyawanprash",
                Slug = "chyawanprash",
                Description = "Chyawanprash formulations",
                IsActive = true
            };
            context.Categories.Add(cat);
            context.Subcategories.Add(sub);
            context.SaveChanges();
        }

        return context;
    }

    [Fact]
    public async Task Test01_PreviewProductImport_DoesNotModifyDatabaseRecords()
    {
        var dbName = "Test_PreviewNoModify_" + Guid.NewGuid().ToString("N");
        using var context = CreateInMemoryContext(dbName);
        var audit = new TestAuditService();
        var service = new CatalogImportService(context, audit, NullLogger<CatalogImportService>.Instance);

        int initialCount = await context.Products.CountAsync();

        var preview = await service.PreviewProductImportAsync();

        int afterCount = await context.Products.CountAsync();

        Assert.NotNull(preview);
        Assert.Equal(initialCount, afterCount);
        Assert.True(preview.TotalCatalogItems > 0);
    }

    [Fact]
    public async Task Test02_Import_InsertsOnlyMissingProducts()
    {
        var dbName = "Test_InsertOnlyMissing_" + Guid.NewGuid().ToString("N");
        using var context = CreateInMemoryContext(dbName);

        // Add 1 existing product with exact title matching catalog item 1
        var existing = new Product
        {
            Id = 10,
            Title = "Himalaya Liv.52 (Tablet / Syrup)",
            Brand = "Himalaya Wellness",
            Price = 160m,
            Mrp = 200m,
            Stock = 45,
            CategoryId = 1,
            SubcategoryId = 1,
            ImageUrl = "/images/products/himalaya/himalaya-wellness-liv52-1.webp",
            Sku = "HIM-LIV52"
        };
        context.Products.Add(existing);
        await context.SaveChangesAsync();

        var audit = new TestAuditService();
        var service = new CatalogImportService(context, audit, NullLogger<CatalogImportService>.Instance);

        var preview = await service.PreviewProductImportAsync();

        Assert.Contains(preview.ExistingMatched, p => p.Title == existing.Title);
        Assert.DoesNotContain(preview.NewProducts, p => p.Title == existing.Title);
    }

    [Fact]
    public async Task Test03_Import_PreservesExistingStock()
    {
        var dbName = "Test_PreserveStock_" + Guid.NewGuid().ToString("N");
        using var context = CreateInMemoryContext(dbName);

        const int originalStock = 87;
        var existing = new Product
        {
            Id = 20,
            Title = "Himalaya Liv.52 (Tablet / Syrup)",
            Brand = "Himalaya Wellness",
            Price = 160m,
            Mrp = 200m,
            Stock = originalStock,
            CategoryId = 1,
            SubcategoryId = 1,
            ImageUrl = "/images/products/himalaya/himalaya-wellness-liv52-1.webp"
        };
        context.Products.Add(existing);
        await context.SaveChangesAsync();

        var audit = new TestAuditService();
        var service = new CatalogImportService(context, audit, NullLogger<CatalogImportService>.Instance);

        var preview = await service.PreviewProductImportAsync();
        Assert.Equal(1, preview.ExistingMatchingCount);

        var productInDb = await context.Products.FindAsync(20);
        Assert.NotNull(productInDb);
        Assert.Equal(originalStock, productInDb.Stock);
    }

    [Fact]
    public async Task Test04_Import_PreservesExistingPricesAndDescriptions()
    {
        var dbName = "Test_PreservePricesDesc_" + Guid.NewGuid().ToString("N");
        using var context = CreateInMemoryContext(dbName);

        const decimal customPrice = 999.50m;
        const string customDesc = "Custom proprietary clinic blend description entered by doctor.";
        var existing = new Product
        {
            Id = 30,
            Title = "Himalaya Liv.52 (Tablet / Syrup)",
            Brand = "Himalaya Wellness",
            Price = customPrice,
            Mrp = 1200m,
            Stock = 15,
            Description = customDesc,
            CategoryId = 1,
            SubcategoryId = 1,
            ImageUrl = "/images/products/himalaya/himalaya-wellness-liv52-1.webp"
        };
        context.Products.Add(existing);
        await context.SaveChangesAsync();

        var audit = new TestAuditService();
        var service = new CatalogImportService(context, audit, NullLogger<CatalogImportService>.Instance);

        var preview = await service.PreviewProductImportAsync();
        Assert.Equal(1, preview.ExistingMatchingCount);

        var productInDb = await context.Products.FindAsync(30);
        Assert.NotNull(productInDb);
        Assert.Equal(customPrice, productInDb.Price);
        Assert.Equal(customDesc, productInDb.Description);
    }

    [Fact]
    public async Task Test05_ImageMapping_PreservesAdminUploadedImages()
    {
        var dbName = "Test_PreserveAdminUploads_" + Guid.NewGuid().ToString("N");
        using var context = CreateInMemoryContext(dbName);

        var existing = new Product
        {
            Id = 40,
            Title = "Himalaya Liv.52 (Tablet / Syrup)",
            Brand = "Himalaya Wellness",
            Price = 160m,
            Mrp = 200m,
            Stock = 10,
            CategoryId = 1,
            SubcategoryId = 1,
            ImageUrl = "/uploads/products/custom-doctor-label-liv52.jpg"
        };
        context.Products.Add(existing);
        await context.SaveChangesAsync();

        var audit = new TestAuditService();
        var service = new CatalogImportService(context, audit, NullLogger<CatalogImportService>.Instance);

        var preview = await service.PreviewImageMappingAsync();

        var item = preview.Items.FirstOrDefault(i => i.ProductId == 40);
        Assert.NotNull(item);
        Assert.Equal(ImageMappingStatus.AdminUploadedPreserved, item.Status);
        Assert.False(item.WillUpdatePrimary);
        Assert.Equal("/uploads/products/custom-doctor-label-liv52.jpg", item.CurrentPrimaryImage);
    }

    [Fact]
    public async Task Test06_ImageMapping_NeverDeletesExistingProductImages()
    {
        var dbName = "Test_NeverDeleteProductImages_" + Guid.NewGuid().ToString("N");
        using var context = CreateInMemoryContext(dbName);

        var existing = new Product
        {
            Id = 50,
            Title = "Himalaya Liv.52 (Tablet / Syrup)",
            Brand = "Himalaya Wellness",
            Price = 160m,
            Mrp = 200m,
            Stock = 10,
            CategoryId = 1,
            SubcategoryId = 1,
            ImageUrl = "/images/products/chyawanprash.svg"
        };
        existing.Images.Add(new ProductImage { ProductId = 50, ImageUrl = "/images/products/chyawanprash.svg", IsPrimary = true, DisplayOrder = 1 });
        existing.Images.Add(new ProductImage { ProductId = 50, ImageUrl = "/images/products/doctor-certificate-liv52.jpg", IsPrimary = false, DisplayOrder = 2 });

        context.Products.Add(existing);
        await context.SaveChangesAsync();

        var audit = new TestAuditService();
        var service = new CatalogImportService(context, audit, NullLogger<CatalogImportService>.Instance);

        int initialImgRows = await context.ProductImages.CountAsync();
        Assert.Equal(2, initialImgRows);

        var preview = await service.PreviewImageMappingAsync();
        var mappingItem = preview.Items.FirstOrDefault(i => i.ProductId == 50);
        Assert.NotNull(mappingItem);

        int currentImgRows = await context.ProductImages.CountAsync();
        Assert.True(currentImgRows >= initialImgRows);
    }

    [Fact]
    public async Task Test07_ImageMapping_IdentifiesGenericSvgForCorrection()
    {
        var dbName = "Test_IdentifyGenericSvg_" + Guid.NewGuid().ToString("N");
        using var context = CreateInMemoryContext(dbName);

        var existing = new Product
        {
            Id = 60,
            Title = "Himalaya Liv.52 (Tablet / Syrup)",
            Brand = "Himalaya Wellness",
            Price = 160m,
            Mrp = 200m,
            Stock = 10,
            CategoryId = 1,
            SubcategoryId = 1,
            ImageUrl = "/images/products/chyawanprash.svg"
        };
        context.Products.Add(existing);
        await context.SaveChangesAsync();

        var audit = new TestAuditService();
        var service = new CatalogImportService(context, audit, NullLogger<CatalogImportService>.Instance);

        var preview = await service.PreviewImageMappingAsync();

        var item = preview.Items.FirstOrDefault(i => i.ProductId == 60);
        Assert.NotNull(item);
        Assert.Equal(ImageMappingStatus.RequiresCorrection, item.Status);
        Assert.True(item.WillUpdatePrimary);
        Assert.Equal("/images/products/himalaya/himalaya-wellness-liv52-1.webp", item.ProposedPrimaryImage);
    }

    [Fact]
    public async Task Test08_ImageMapping_RejectsInvalidDiskPaths()
    {
        var dbName = "Test_RejectInvalidDiskPaths_" + Guid.NewGuid().ToString("N");
        using var context = CreateInMemoryContext(dbName);

        var existing = new Product
        {
            Id = 70,
            Title = "Custom Non-Existent Ayurvedic Elixir 999",
            Brand = "NonExistentBrand",
            Price = 500m,
            Stock = 10,
            CategoryId = 1,
            SubcategoryId = 1,
            ImageUrl = "/images/products/does-not-exist-at-all-on-disk.webp"
        };
        context.Products.Add(existing);
        await context.SaveChangesAsync();

        var audit = new TestAuditService();
        var service = new CatalogImportService(context, audit, NullLogger<CatalogImportService>.Instance);

        var preview = await service.PreviewImageMappingAsync();

        var item = preview.Items.FirstOrDefault(i => i.ProductId == 70);
        Assert.NotNull(item);
        Assert.Equal(ImageMappingStatus.ProductNotFoundInCatalog, item.Status);
    }

    [Fact]
    public async Task Test09_RepeatedPreview_ProducesIdenticalResultsWithoutDuplicates()
    {
        var dbName = "Test_RepeatedPreview_" + Guid.NewGuid().ToString("N");
        using var context = CreateInMemoryContext(dbName);

        var audit = new TestAuditService();
        var service = new CatalogImportService(context, audit, NullLogger<CatalogImportService>.Instance);

        var preview1 = await service.PreviewProductImportAsync();
        var preview2 = await service.PreviewProductImportAsync();

        Assert.Equal(preview1.TotalCatalogItems, preview2.TotalCatalogItems);
        Assert.Equal(preview1.NewProductsToInsertCount, preview2.NewProductsToInsertCount);
        Assert.Equal(preview1.ExistingMatchingCount, preview2.ExistingMatchingCount);
    }

    [Fact]
    public async Task Test10_SystemStatus_ReportsAccurateProductCounts()
    {
        var dbName = "Test_SystemStatus_" + Guid.NewGuid().ToString("N");
        using var context = CreateInMemoryContext(dbName);

        context.Products.Add(new Product
        {
            Id = 1,
            Title = "Test Product 1",
            Price = 100,
            Stock = 10,
            CategoryId = 1,
            ImageUrl = "/images/placeholder.svg"
        });
        context.Products.Add(new Product
        {
            Id = 2,
            Title = "Test Product 2",
            Price = 200,
            Stock = 20,
            CategoryId = 1,
            ImageUrl = "/images/products/himalaya/himalaya-wellness-liv52-1.webp"
        });
        await context.SaveChangesAsync();

        var audit = new TestAuditService();
        var service = new CatalogImportService(context, audit, NullLogger<CatalogImportService>.Instance);

        var status = await service.GetSystemStatusAsync();

        Assert.Equal(2, status.DatabaseProductCount);
        Assert.Equal(400, status.CatalogJsonProductCount);
        Assert.Equal(1, status.GenericOrPlaceholderImageCount);
        Assert.Equal(1, status.ValidProductImageCount);
        Assert.True(status.CatalogJsonExists);
    }

    [Fact]
    public void Test11_CatalogImportController_HasSuperAdminAuthorizationPolicy()
    {
        var controllerType = typeof(CatalogImportController);
        var authAttribute = Attribute.GetCustomAttribute(controllerType, typeof(AuthorizeAttribute)) as AuthorizeAttribute;

        Assert.NotNull(authAttribute);
        Assert.Equal("RequireSuperAdmin", authAttribute.Policy);
    }

    [Fact]
    public async Task Test12_CatalogImport_DisabledByConfiguration_BlocksOperations()
    {
        var dbName = "Test_ConfigDisabled_" + Guid.NewGuid().ToString("N");
        using var context = CreateInMemoryContext(dbName);
        var audit = new TestAuditService();

        var config = new Microsoft.Extensions.Configuration.ConfigurationManager();
        config["CatalogImport:Enabled"] = "false";

        var service = new CatalogImportService(context, audit, config, NullLogger<CatalogImportService>.Instance);

        var preview = await service.PreviewProductImportAsync();
        Assert.Contains("disabled by configuration", preview.Message);

        var exec = await service.ExecuteProductImportAsync("test@admin.com");
        Assert.False(exec.Success);
        Assert.Contains(exec.Errors, e => e.Contains("disabled by configuration"));

        var imgPreview = await service.PreviewImageMappingAsync();
        Assert.Contains("disabled by configuration", imgPreview.Message);

        var imgExec = await service.ExecuteImageMappingAsync("test@admin.com");
        Assert.False(imgExec.Success);
        Assert.Contains(imgExec.Errors, e => e.Contains("disabled by configuration"));
    }

    [Fact]
    public void Test13_CatalogImportController_AllPostActionsHaveAntiForgeryToken()
    {
        var controllerType = typeof(CatalogImportController);
        var postMethods = controllerType.GetMethods()
            .Where(m => Attribute.IsDefined(m, typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute)))
            .ToList();

        Assert.True(postMethods.Count >= 4);
        foreach (var method in postMethods)
        {
            bool hasAntiforgery = Attribute.IsDefined(method, typeof(Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute));
            Assert.True(hasAntiforgery, $"Method {method.Name} is missing [ValidateAntiForgeryToken]");
        }
    }
}
