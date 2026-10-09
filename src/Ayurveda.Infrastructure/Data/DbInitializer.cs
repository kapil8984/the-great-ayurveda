using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ayurveda.Domain.Entities;
using Ayurveda.Domain.Enums;

namespace Ayurveda.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(
        AyurvedaDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<int>> roleManager,
        ILogger logger)
    {
        try
        {
            // Seed Roles
            var roles = new[]
            {
                UserRole.Customer.ToString(),
                UserRole.Doctor.ToString(),
                UserRole.SuperAdmin.ToString(),
                UserRole.CatalogManager.ToString(),
                UserRole.SupportAgent.ToString(),
                UserRole.FinanceManager.ToString()
            };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole<int>(role));
                    logger.LogInformation("Seeded Identity Role: {Role}", role);
                }
            }

            // Seed Categories if empty
            if (!await context.Categories.AnyAsync())
            {
                var categories = new List<Category>
                {
                    new() { Name = "Immunity & Rasayana", Description = "Traditional formulations to support vitality and natural defenses.", ImageUrl = "/images/categories/immunity.svg" },
                    new() { Name = "Digestive Care", Description = "Holistic herbal churnas and gut wellness formulations.", ImageUrl = "/images/categories/digestion.svg" },
                    new() { Name = "Joint & Muscle Care", Description = "Therapeutic massage oils and soothing herbal balms.", ImageUrl = "/images/categories/joints.svg" },
                    new() { Name = "Skin Care", Description = "Pure herbal ubtans, Kumkumadi and Ayurvedic face oils.", ImageUrl = "/images/categories/skin.svg" },
                    new() { Name = "Hair Care", Description = "Bhringraj, Amla and cold-pressed hair wellness oils.", ImageUrl = "/images/categories/hair.svg" },
                    new() { Name = "Stress, Sleep & Brain", Description = "Pure Ashwagandha, Brahmi and calming botanical teas.", ImageUrl = "/images/categories/sleep.svg" },
                    new() { Name = "Diabetes & Metabolism Support", Description = "Traditional herbs supporting healthy blood sugar balance.", ImageUrl = "/images/categories/metabolism.svg" },
                    new() { Name = "Women's Wellness", Description = "Shatavari, hormonal harmony and restorative tonics.", ImageUrl = "/images/categories/womens.svg" },
                    new() { Name = "Herbs & Raw Powders", Description = "Single-herb powders, roots and ethically harvested botanicals.", ImageUrl = "/images/categories/herbs.svg" },
                    new() { Name = "Panchakarma & Therapy Kits", Description = "Authentic Nasya, Abhyanga and wellness accessories.", ImageUrl = "/images/categories/panchakarma.svg" },
                    new() { Name = "Yoga & Wellness", Description = "Cotton yoga mats, meditation props and aromatherapy.", ImageUrl = "/images/categories/yoga.svg" },
                    new() { Name = "Honey, Ghee & Natural Foods", Description = "A2 Gir Cow Bilona Ghee and wild raw forest honey.", ImageUrl = "/images/categories/foods.svg" }
                };

                await context.Categories.AddRangeAsync(categories);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded 12 core Ayurvedic categories.");
            }

            // Seed Products if empty
            if (!await context.Products.AnyAsync())
            {
                var categories = await context.Categories.ToListAsync();
                var immunityCat = categories.FirstOrDefault(c => c.Name.Contains("Immunity"))?.Id ?? categories[0].Id;
                var digestiveCat = categories.FirstOrDefault(c => c.Name.Contains("Digestive"))?.Id ?? categories[0].Id;
                var jointsCat = categories.FirstOrDefault(c => c.Name.Contains("Joint"))?.Id ?? categories[0].Id;
                var hairCat = categories.FirstOrDefault(c => c.Name.Contains("Hair"))?.Id ?? categories[0].Id;
                var stressCat = categories.FirstOrDefault(c => c.Name.Contains("Stress"))?.Id ?? categories[0].Id;

                var initialProducts = new List<Product>
                {
                    new()
                    {
                        Title = "Organic Amla Chyawanprash Special",
                        Brand = "Great Ayurveda Labs",
                        Description = "Traditional Ayurvedic rasayana with 40+ wildcrafted herbs, raw honey and A2 cow ghee. Traditionally used to support seasonal immunity and vitality.",
                        Price = 450.00m,
                        Mrp = 550.00m,
                        DiscountPercent = 18,
                        Stock = 50,
                        CategoryId = immunityCat,
                        ImageUrl = "/images/products/chyawanprash.svg",
                        AverageRating = 4.8,
                        TraditionallyUsedFor = "Immunity, Vitality, Respiratory Health",
                        Ingredients = "Wild Amla, Ashwagandha, Shatavari, Pipali, Cardamom, Raw Honey, A2 Ghee",
                        HowToUse = "1-2 teaspoons twice daily with warm milk or lukewarm water.",
                        Precautions = "Consult a doctor before use if diabetic.",
                        IsActive = true
                    },
                    new()
                    {
                        Title = "Triphala Churna Digestive Powder",
                        Brand = "Great Ayurveda Labs",
                        Description = "Pure balanced tri-doshic blend of Haritaki, Bibhitaki, and Amalaki. Gently supports digestion and natural detoxification.",
                        Price = 220.00m,
                        Mrp = 275.00m,
                        DiscountPercent = 20,
                        Stock = 75,
                        CategoryId = digestiveCat,
                        ImageUrl = "/images/products/triphala.svg",
                        AverageRating = 4.7,
                        TraditionallyUsedFor = "Digestion, Regularity, Gut Cleansing",
                        Ingredients = "Terminalia chebula (Haritaki), Terminalia bellirica (Bibhitaki), Emblica officinalis (Amalaki)",
                        HowToUse = "1 teaspoon before bedtime with warm water.",
                        Precautions = "Not recommended during acute diarrhea.",
                        IsActive = true
                    },
                    new()
                    {
                        Title = "Mahanarayan Muscle & Joint Oil",
                        Brand = "Great Ayurveda Labs",
                        Description = "Authentic classical oil prepared with sesame base and 30+ botanical extracts. Traditionally used for comforting stiff joints and active muscles.",
                        Price = 380.00m,
                        Mrp = 450.00m,
                        DiscountPercent = 15,
                        Stock = 40,
                        CategoryId = jointsCat,
                        ImageUrl = "/images/products/mahanarayan.svg",
                        AverageRating = 4.9,
                        TraditionallyUsedFor = "Joint Flexibility, Muscle Ease, Vata Balance",
                        Ingredients = "Sesame oil, Dashamula, Ashwagandha, Bala, Shatavari, Camphor",
                        HowToUse = "Warm slightly and gently massage into affected areas.",
                        Precautions = "For external application only. Do not apply on open wounds.",
                        IsActive = true
                    },
                    new()
                    {
                        Title = "Kuntal Care Bhringraj Hair Wellness Oil",
                        Brand = "Great Ayurveda Labs",
                        Description = "Cold-pressed virgin coconut and sesame oil infused with Bhringraj, Brahmi and Hibiscus to nourish scalp and hair roots.",
                        Price = 340.00m,
                        Mrp = 400.00m,
                        DiscountPercent = 15,
                        Stock = 60,
                        CategoryId = hairCat,
                        ImageUrl = "/images/products/hair-oil.svg",
                        AverageRating = 4.6,
                        TraditionallyUsedFor = "Scalp Health, Hair Strength, Relaxation",
                        Ingredients = "Eclipta alba (Bhringraj), Bacopa monnieri (Brahmi), Amla, Coconut Oil, Sesame Oil",
                        HowToUse = "Apply to scalp and leave on for at least 1 hour or overnight before washing.",
                        Precautions = "For external use only.",
                        IsActive = true
                    },
                    new()
                    {
                        Title = "Pure Ashwagandha Root Extract Capsules",
                        Brand = "Great Ayurveda Labs",
                        Description = "Standardized Withania somnifera root extract. Traditionally used in Ayurveda as a rejuvenating adaptogen to support restful sleep and stress response.",
                        Price = 490.00m,
                        Mrp = 599.00m,
                        DiscountPercent = 18,
                        Stock = 35,
                        CategoryId = stressCat,
                        ImageUrl = "/images/products/ashwagandha.svg",
                        AverageRating = 4.9,
                        TraditionallyUsedFor = "Stress Relief, Sound Sleep, Physical Endurance",
                        Ingredients = "Organic Ashwagandha root extract (5% Withanolides)",
                        HowToUse = "1 capsule twice daily after meals with warm water or milk.",
                        Precautions = "Pregnant or nursing women should consult a healthcare professional.",
                        IsActive = true
                    }
                };

                await context.Products.AddRangeAsync(initialProducts);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded initial showcase products.");
            }

            // Seed Yoga poses if empty
            if (!await context.Yogas.AnyAsync())
            {
                var catId = (await context.Categories.FirstAsync()).Id;
                var initialYoga = new List<Yoga>
                {
                    new()
                    {
                        YogaName = "Surya Namaskar (Sun Salutation)",
                        Description = "A graceful sequence of 12 powerful postures connecting breath and movement to awaken solar energy.",
                        Instructions = "Stand at the front edge of the mat with palms folded in Pranamasana. Flow through Hastautthanasana, Padahastasana, Ashwa Sanchalanasana, and Bhujangasana with synchronized breath.",
                        ImageUrl = "/images/yoga/surya-namaskar.svg",
                        VideoUrl = "https://www.youtube.com/embed/sample",
                        CategoryId = catId,
                        BenefitsJson = "[\"Boosts metabolic fire (Agni)\", \"Improves spinal flexibility\", \"Enhances prana circulation\", \"Calms the mind\"]",
                        AverageRating = 5.0
                    },
                    new()
                    {
                        YogaName = "Vrikshasana (Tree Pose)",
                        Description = "A classic standing balance posture fostering equilibrium, grounding, and focused presence.",
                        Instructions = "Shift weight to the left foot. Place the right sole against the inner left thigh or calf (avoiding the knee). Bring palms to prayer at heart center.",
                        ImageUrl = "/images/yoga/vrikshasana.svg",
                        VideoUrl = "https://www.youtube.com/embed/sample",
                        CategoryId = catId,
                        BenefitsJson = "[\"Strengthens ankles and calves\", \"Improves neuromuscular coordination\", \"Promotes mental stillness\"]",
                        AverageRating = 4.8
                    }
                };

                await context.Yogas.AddRangeAsync(initialYoga);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded foundational yoga postures.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during database seeding.");
            throw;
        }
    }
}
