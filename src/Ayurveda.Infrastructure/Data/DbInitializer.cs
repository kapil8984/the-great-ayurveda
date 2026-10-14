using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Ayurveda.Domain.Entities;
using Ayurveda.Domain.Enums;

namespace Ayurveda.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(
        AyurvedaDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<int>> roleManager,
        ILogger logger,
        Microsoft.Extensions.Configuration.IConfiguration? configuration = null,
        Microsoft.Extensions.Hosting.IHostEnvironment? environment = null)
    {
        try
        {
            bool isDev = environment?.IsDevelopment() ?? true;
            bool isProd = environment?.IsProduction() ?? false;
            bool seedEnabled = configuration?.GetValue<bool?>("SeedData:Enabled") ?? isDev;

            // 0. Schema Alignment, Foreign Key Repairs & Data Sync
            await AlignDatabaseSchemaAndFixDataAsync(context, userManager, roleManager, logger, isDev);

            // 1. Seed Roles
            var roles = new[]
            {
                "Admin",
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

            // SuperAdmin Provisioning (Idempotent - never resets existing user password)
            var adminUser = await userManager.FindByEmailAsync("admin@thegreatayurveda.com")
                         ?? await userManager.FindByEmailAsync("admin@ayurveda.com");

            if (adminUser == null)
            {
                string adminPassword;
                if (isProd)
                {
                    adminPassword = configuration?["Admin:InitialPassword"]
                        ?? Environment.GetEnvironmentVariable("Admin__InitialPassword") ?? "";

                    if (string.IsNullOrWhiteSpace(adminPassword))
                    {
                        logger.LogCritical("FATAL: 'Admin__InitialPassword' environment variable or configuration is missing in Production. An initial administrative password is required. Application startup aborted.");
                        throw new InvalidOperationException("FATAL: 'Admin__InitialPassword' environment variable is required in Production. Startup aborted.");
                    }
                }
                else
                {
                    adminPassword = configuration?["Admin:InitialPassword"]
                        ?? configuration?["DevDemo:AdminPassword"]
                        ?? "DevAuth_Vaidya_2026!#";
                }

                var primaryAdminEmail = configuration?["Admin:InitialEmail"] ?? "admin@thegreatayurveda.com";
                adminUser = new ApplicationUser
                {
                    UserName = primaryAdminEmail,
                    Email = primaryAdminEmail,
                    FullName = "System SuperAdmin",
                    EmailConfirmed = true,
                    IsActive = true,
                    LockoutEnabled = false,
                    CreatedAt = DateTime.UtcNow
                };

                var adminRes = await userManager.CreateAsync(adminUser, adminPassword);
                if (adminRes.Succeeded)
                {
                    await userManager.AddToRolesAsync(adminUser, new[] { "SuperAdmin", "Admin", "CatalogManager", "SupportAgent", "FinanceManager" });
                    logger.LogInformation("Seeded initial SuperAdmin account {Email}", primaryAdminEmail);
                }
                else
                {
                    var errors = string.Join("; ", adminRes.Errors.Select(e => e.Description));
                    logger.LogCritical("FATAL: Failed to create initial SuperAdmin: {Errors}", errors);
                    throw new InvalidOperationException($"FATAL: Failed to create initial SuperAdmin: {errors}");
                }
            }
            else
            {
                logger.LogInformation("Admin user {Email} already exists. Retaining credentials without modification.", adminUser.Email);
            }

            // Seed subordinate staff roles only in Development
            if (isDev)
            {
                var devStaffPassword = configuration?["DevDemo:AdminPassword"] ?? "DevAuth_Vaidya_2026!#";
                var devStaff = new (string Email, string Name, string[] Roles)[]
                {
                    ("catalog@thegreatayurveda.com", "Catalog Manager", new[] { "CatalogManager" }),
                    ("support@thegreatayurveda.com", "Support Agent", new[] { "SupportAgent" }),
                    ("finance@thegreatayurveda.com", "Finance Manager", new[] { "FinanceManager" })
                };

                foreach (var stf in devStaff)
                {
                    var existingStf = await userManager.FindByEmailAsync(stf.Email);
                    if (existingStf == null)
                    {
                        var newUser = new ApplicationUser
                        {
                            UserName = stf.Email,
                            Email = stf.Email,
                            FullName = stf.Name,
                            EmailConfirmed = true,
                            IsActive = true,
                            LockoutEnabled = false,
                            CreatedAt = DateTime.UtcNow
                        };
                        var res = await userManager.CreateAsync(newUser, devStaffPassword);
                        if (res.Succeeded)
                        {
                            await userManager.AddToRolesAsync(newUser, stf.Roles);
                            logger.LogInformation("Seeded dev staff {Email} ({Roles})", stf.Email, string.Join(", ", stf.Roles));
                        }
                    }
                }
            }

            // If SeedData:Enabled is false, skip remaining catalog/demo seeding
            if (!seedEnabled)
            {
                logger.LogInformation("SeedData:Enabled is false. Skipping catalog and demo data seeding.");
                return;
            }

            // Item 2: Migrate any existing users with legacy "Admin" role to "SuperAdmin"
            var legacyAdmins = await userManager.GetUsersInRoleAsync("Admin");
            foreach (var legacyAdmin in legacyAdmins)
            {
                if (!await userManager.IsInRoleAsync(legacyAdmin, "SuperAdmin"))
                {
                    await userManager.AddToRoleAsync(legacyAdmin, "SuperAdmin");
                    logger.LogInformation("Migrated legacy Admin user {Email} to SuperAdmin role.", legacyAdmin.Email);
                }
            }

            // 2. Seed 26 Categories
            var categoryDefinitions = new (string Name, string Description, string ImageUrl)[]
            {
                ("Immunity & Rasayana", "Traditional formulations to support vitality, Ojas, and natural defenses.", "/images/categories/immunity-rasayana.svg"),
                ("Digestive Care", "Holistic herbal churnas, deepana-pachana herbs, and gut wellness formulations.", "/images/categories/digestive-care.svg"),
                ("Joint & Muscle Care", "Therapeutic classical massage oils, tailas, and soothing herbal balms.", "/images/categories/joint-muscle-care.svg"),
                ("Skin Care & Complexion", "Pure herbal ubtans, Kumkumadi face oils, and complexion balms.", "/images/categories/skin-care.svg"),
                ("Hair Care & Scalp Health", "Bhringraj, Amla, Brahmi, and cold-pressed hair wellness oils and shampoos.", "/images/categories/hair-care.svg"),
                ("Stress, Sleep & Brain", "Pure Ashwagandha, Medhya Rasayanas, and calming botanical teas.", "/images/categories/stress-sleep-brain.svg"),
                ("Diabetic Care & Metabolism", "Traditional herbs supporting healthy blood sugar and metabolic Agni.", "/images/categories/diabetic-care.svg"),
                ("Women's Wellness", "Shatavari, hormonal harmony, cycle balance, and restorative tonics.", "/images/categories/womens-wellness.svg"),
                ("Men's Wellness", "Classical botanical tonics supporting masculine vitality, stamina, and Ojas.", "/images/categories/mens-wellness.svg"),
                ("Herbal Teas & Kadhas", "Therapeutic whole leaf infusions, Ayurvedic spice kadhas, and botanical brews.", "/images/categories/herbal-teas.svg"),
                ("Ghee & Honey", "Vedic A2 Gir Cow Bilona Ghee and wild raw multi-flora forest honey.", "/images/categories/ghee-honey.svg"),
                ("Massage Oils & Tailas", "Traditional Abhyanga oils infused through classical Sneha Kalpana decoction.", "/images/categories/massage-oils.svg"),
                ("Ubtan & Bath Rituals", "Ancient Snana churna scrubs, turmeric bathing pastes, and natural clay polishes.", "/images/categories/ubtan-bath.svg"),
                ("Aromatherapy & Essential Oils", "Pure steam-distilled Indian botanical essences, natural attars, and diffusers.", "/images/categories/aromatherapy.svg"),
                ("Baby Care & Motherhood", "Gentle traditional formulations for infants and nursing mothers.", "/images/categories/baby-care.svg"),
                ("Eye Care & Netra Shodhana", "Ocular cooling botanical washes, classical Triphala eye solutions, and rose hydrosols.", "/images/categories/eye-care.svg"),
                ("Oral Care & Danta Shodhana", "Herbal tooth powders, Bakul gum tonics, and classical oil pulling blends.", "/images/categories/oral-care.svg"),
                ("Weight Management & Metabolism", "Traditional formulations to balance Medo Dhatu and kindle digestive fire.", "/images/categories/weight-management.svg"),
                ("Pooja, Havan & Sacred Botanicals", "Ethically prepared sacred samagri, forest resin dhoop, and brass ritual vessels.", "/images/categories/pooja-havan.svg"),
                ("Ayurvedic Books & Literature", "Authoritative guides on Charaka Samhita, home remedies, Ahara diet, and Dinacharya.", "/images/categories/ayurvedic-books.svg"),
                ("Yoga Accessories & Props", "Eco-friendly organic cotton mats, meditation zafus, and natural cork blocks.", "/images/categories/yoga-accessories.svg"),
                ("Gift Hampers & Wellness Sets", "Thoughtfully curated festive gifting boxes of authentic Ayurvedic self-care essentials.", "/images/categories/gift-hampers.svg"),
                ("Festival Specials & Seasonal Ritu", "Ritucharya seasonal cleansing kits and traditional festive wellness elixirs.", "/images/categories/festival-specials.svg"),
                ("Herbs & Raw Single Powders", "Single-herb churnas, ethically harvested roots, seeds, and sun-dried barks.", "/images/categories/herbs-powders.svg"),
                ("Panchakarma & Therapy Kits", "Authentic Nasya oils, Kansa Vataki massage bowls, and Neti pot accessories.", "/images/categories/panchakarma-kits.svg"),
                ("Respiratory & Prana Care", "Classical Sitopaladi, Talisadi churnas, and herbal chest calming balms.", "/images/categories/respiratory-care.svg")
            };

            foreach (var def in categoryDefinitions)
            {
                var existing = await context.Categories.FirstOrDefaultAsync(c => c.Name == def.Name);
                if (existing == null)
                {
                    existing = await context.Categories.FirstOrDefaultAsync(c => def.Name.StartsWith(c.Name) || c.Name.StartsWith(def.Name));
                }

                if (existing == null)
                {
                    context.Categories.Add(new Category
                    {
                        Name = def.Name,
                        Description = def.Description,
                        ImageUrl = def.ImageUrl,
                        IsActive = true
                    });
                }
                else
                {
                    existing.Name = def.Name;
                    existing.Description = def.Description;
                    existing.ImageUrl = def.ImageUrl;
                    existing.IsActive = true;
                }
            }
            await context.SaveChangesAsync();

            // 3. Seed 78 Subcategories (3 per category)
            var subcategoryMap = new Dictionary<string, string[]>
            {
                ["Immunity & Rasayana"] = new[] { "Chyawanprash", "Herbal Tonics & Asavas", "Giloy & Tulsi Formulations" },
                ["Digestive Care"] = new[] { "Digestive Churnas", "Gut Wellness Tablets", "Deepana Herbal Teas" },
                ["Joint & Muscle Care"] = new[] { "Therapeutic Massage Oils", "Herbal Pain Balms", "Guggulu Bone Tablets" },
                ["Skin Care & Complexion"] = new[] { "Kumkumadi Oils", "Herbal Face Ubtans", "Complexion Elixirs" },
                ["Hair Care & Scalp Health"] = new[] { "Bhringraj Tailas", "Herbal Kesh Powders", "Scalp Cleansers" },
                ["Stress, Sleep & Brain"] = new[] { "Ashwagandha Elixirs", "Brahmi Medhya Tonics", "Sleep Calming Infusions" },
                ["Diabetic Care & Metabolism"] = new[] { "Karela Jamun Kwath", "Madhunashini Tablets", "Metabolic Balance Teas" },
                ["Women's Wellness"] = new[] { "Shatavari Rasayana", "Menstrual Cycle Tonics", "Postnatal Restorative Care" },
                ["Men's Wellness"] = new[] { "Shilajit Gold Resin", "Vajikarana Rasayana", "Stamina & Energy Herbs" },
                ["Herbal Teas & Kadhas"] = new[] { "Classical Spiced Kadha", "Tulsi Green Infusions", "Chamomile Shankhpushpi Tea" },
                ["Ghee & Honey"] = new[] { "A2 Gir Cow Bilona Ghee", "Raw Forest Honey", "Medicated Herbal Ghrithas" },
                ["Massage Oils & Tailas"] = new[] { "Mahanarayan Taila", "Dhanwantharam Oil", "Bala Thailam Body Oil" },
                ["Ubtan & Bath Rituals"] = new[] { "Royal Bridal Ubtan", "Sandalwood Body Polish", "Snana Ayurvedic Bath Powders" },
                ["Aromatherapy & Essential Oils"] = new[] { "Pure Essential Oils", "Natural Herbal Attars", "Ceramic Aroma Diffusers" },
                ["Baby Care & Motherhood"] = new[] { "Balashwagandhadi Baby Oil", "Gentle Infant Ubtan", "Mother Lactation Tonics" },
                ["Eye Care & Netra Shodhana"] = new[] { "Triphala Netra Wash", "Herbal Cooling Eye Drops", "Cucumber Rose Hydrosol" },
                ["Oral Care & Danta Shodhana"] = new[] { "Danta Manjan Powder", "Herbal Oil Pulling Rinse", "Clove Bakul Gum Gel" },
                ["Weight Management & Metabolism"] = new[] { "Medohar Guggulu", "Metabolic Kindling Kwath", "Triphala Guggulu Tablets" },
                ["Pooja, Havan & Sacred Botanicals"] = new[] { "Vedic Havan Samagri", "Pure Guggul Dhoop Cones", "Gomaya Natural Diyas" },
                ["Ayurvedic Books & Literature"] = new[] { "Classical Text Translations", "Home Remedies Handbooks", "Ayurvedic Nutrition Guides" },
                ["Yoga Accessories & Props"] = new[] { "Organic Cotton Yoga Mats", "Cork Yoga Blocks & Straps", "Meditation Buckwheat Cushions" },
                ["Gift Hampers & Wellness Sets"] = new[] { "Royal Rasayana Hampers", "Abhyanga Self-Care Bundles", "Bridal Glow Gift Sets" },
                ["Festival Specials & Seasonal Ritu"] = new[] { "Diwali Ojas Celebration Kit", "Navratri Sattvic Energy Blends", "Monsoon Shodhana Detox Box" },
                ["Herbs & Raw Single Powders"] = new[] { "Single Herb Roots", "Fine Botanical Powders", "Sacred Herbal Seeds" },
                ["Panchakarma & Therapy Kits"] = new[] { "Nasya Drops & Applicators", "Bronze Kansa Vataki Bowls", "Copper Jal Neti Pots" },
                ["Respiratory & Prana Care"] = new[] { "Sitopaladi Formulations", "Talisadi Herbal Powders", "Eucalyptus Herb Inhalers" }
            };

            var categoriesInDb = await context.Categories.ToListAsync();
            foreach (var cat in categoriesInDb)
            {
                if (subcategoryMap.TryGetValue(cat.Name, out var subNames))
                {
                    foreach (var subName in subNames)
                    {
                        var existingSub = await context.Subcategories.FirstOrDefaultAsync(s => s.CategoryId == cat.Id && s.Name == subName);
                        if (existingSub == null)
                        {
                            context.Subcategories.Add(new Subcategory
                            {
                                CategoryId = cat.Id,
                                Name = subName,
                                Slug = subName.ToLower().Replace(" ", "-").Replace("&", "and").Replace("/", "-"),
                                Description = $"Authentic Ayurvedic {subName} formulations.",
                                IsActive = true
                            });
                        }
                    }
                }
            }
            await context.SaveChangesAsync();

            // 4. Seed 300+ Products from catalog_300plus.json
            var currentProductCount = await context.Products.CountAsync();
            if (currentProductCount < 300)
            {
                var subcategories = await context.Subcategories.Include(s => s.Category).ToListAsync();
                
                string[] candidates = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "catalog_300plus.json"),
                    Path.Combine(AppContext.BaseDirectory, "Data", "catalog_300plus.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "catalog_300plus.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "src", "Ayurveda.Infrastructure", "Data", "catalog_300plus.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "Ayurveda.Infrastructure", "Data", "catalog_300plus.json"),
                    @"D:\TheGreatAyurveda\The-Great-Ayurveda\src\Ayurveda.Infrastructure\Data\catalog_300plus.json"
                };

                string? jsonPath = candidates.FirstOrDefault(File.Exists);
                List<ProductJsonItem>? catalogData = null;
                if (!string.IsNullOrEmpty(jsonPath))
                {
                    var jsonContent = await File.ReadAllTextAsync(jsonPath);
                    catalogData = System.Text.Json.JsonSerializer.Deserialize<List<ProductJsonItem>>(jsonContent, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }

                if (catalogData != null && catalogData.Any())
                {
                    foreach (var item in catalogData)
                    {
                        var sub = subcategories.FirstOrDefault(s => s.Category.Name == item.CategoryName && s.Name == item.SubcategoryName)
                                  ?? subcategories.FirstOrDefault(s => s.Category.Name == item.CategoryName)
                                  ?? subcategories.First();

                        var productExists = await context.Products.AnyAsync(p => p.Title == item.Title);
                        if (!productExists)
                        {
                            var s = GenerateProductSlug(item.Title);
                            var product = new Product
                            {
                                Title = item.Title,
                                Brand = item.Brand,
                                Description = item.Description,
                                Price = item.Price,
                                Mrp = item.Mrp,
                                DiscountPercent = item.Mrp > 0 ? (int)Math.Round((1.0m - (item.Price / item.Mrp)) * 100) : 0,
                                Stock = item.Stock,
                                CategoryId = sub.CategoryId,
                                SubcategoryId = sub.Id,
                                ImageUrl = $"/images/products/{s}.webp",
                                AverageRating = item.Rating,
                                Sku = item.Sku,
                                Tags = item.Tags,
                                TraditionallyUsedFor = item.TraditionallyUsedFor,
                                Ingredients = item.Ingredients,
                                HowToUse = item.HowToUse,
                                Precautions = item.Precautions,
                                IsPrescriptionRequired = item.IsPrescriptionRequired,
                                IsActive = true
                            };

                            if (item.Variants != null && item.Variants.Any())
                            {
                                foreach (var v in item.Variants)
                                {
                                    product.Variants.Add(new ProductVariant
                                    {
                                        Title = v.Title,
                                        SizeOrPack = v.SizeOrPack,
                                        Price = v.Price,
                                        Mrp = v.Mrp,
                                        StockQuantity = v.Stock,
                                        Sku = $"{product.Sku}-{v.SizeOrPack.Replace(" ", "")}"
                                    });
                                }
                            }

                            if (item.Reviews != null && item.Reviews.Any())
                            {
                                foreach (var r in item.Reviews)
                                {
                                    product.Reviews.Add(new ProductReview
                                    {
                                        UserId = 1,
                                        Rating = r.Rating,
                                        ReviewText = r.Text,
                                        HelpfulVotes = r.Helpful,
                                        IsVerifiedPurchase = true,
                                        CreatedAt = DateTime.UtcNow.AddDays(-r.DaysAgo)
                                    });
                                }
                            }

                            if (item.Faqs != null && item.Faqs.Any())
                            {
                                foreach (var f in item.Faqs)
                                {
                                    product.Questions.Add(new ProductQuestion
                                    {
                                        QuestionText = f.Question,
                                        AnswerText = f.Answer,
                                        AskedBy = f.AskedBy,
                                        AnsweredBy = "Senior Ayurvedic Vaidya, Great Ayurveda Labs",
                                        CreatedAt = DateTime.UtcNow.AddDays(-10)
                                    });
                                }
                            }

                            context.Products.Add(product);
                        }
                    }
                    await context.SaveChangesAsync();
                    logger.LogInformation("Seeded {Count} rich Ayurvedic products across 26 categories.", catalogData.Count);
                }
            }

            // Ensure all product images and inventory distribution
            var allProducts = await context.Products.ToListAsync();
            int prdIndex = 0;
            foreach (var p in allProducts)
            {
                var s = GenerateProductSlug(p.Title);
                p.ImageUrl = $"/images/products/{s}.webp";
                if (prdIndex % 15 == 0) p.Stock = 0; // Out of stock
                else if (prdIndex % 7 == 0) p.Stock = 3; // Low stock
                prdIndex++;
            }
            await context.SaveChangesAsync();

            // 5. Seed Promotional Coupons
            if (!await context.Coupons.AnyAsync())
            {
                var coupons = new List<Coupon>
                {
                    new() { Code = "AYUR10", DiscountType = DiscountType.Percentage, DiscountAmount = 10, MinOrderAmount = 499, MaxDiscountAmount = 250, IsActive = true },
                    new() { Code = "TGAFIRST", DiscountType = DiscountType.Flat, DiscountAmount = 100, MinOrderAmount = 599, MaxDiscountAmount = 100, IsActive = true },
                    new() { Code = "VEDA20", DiscountType = DiscountType.Percentage, DiscountAmount = 20, MinOrderAmount = 1499, MaxDiscountAmount = 500, IsActive = true }
                };
                await context.Coupons.AddRangeAsync(coupons);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded active discount coupons.");
            }

            // 6. Seed Yoga poses if empty
            if (!await context.Yogas.AnyAsync())
            {
                var firstCatId = (await context.Categories.FirstAsync()).Id;
                var initialYoga = new List<Yoga>
                {
                    new()
                    {
                        YogaName = "Surya Namaskar (Sun Salutation)",
                        Description = "A graceful sequence of 12 powerful postures connecting breath and movement to awaken solar energy.",
                        Instructions = "Stand at the front edge of the mat with palms folded in Pranamasana. Flow through Hastautthanasana, Padahastasana, Ashwa Sanchalanasana, and Bhujangasana with synchronized breath.",
                        ImageUrl = "/images/yoga/surya-namaskar.svg",
                        VideoUrl = "https://www.youtube.com/embed/sample",
                        CategoryId = firstCatId,
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
                        CategoryId = firstCatId,
                        BenefitsJson = "[\"Strengthens ankles and calves\", \"Improves neuromuscular coordination\", \"Promotes mental stillness\"]",
                        AverageRating = 4.8
                    }
                };

                await context.Yogas.AddRangeAsync(initialYoga);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded foundational yoga postures.");
            }

            // 7. Seed and Upsert 6 Hero & 4 Middle original WebP banners
            await SeedOrUpdateBannersAsync(context, logger);

            // 7b. Seed and Upsert Festival Coupons
            await SeedFestivalOffersAsync(context, logger);

            // 8. Seed 24 Fictional Ayurvedic Doctors (Phase 7)
            await SeedDoctorsAsync(context, userManager, logger, isDev, configuration?["DevDemo:DoctorCredential"]);

            // Ensure doctor avatars are unique and assigned
            var allDoctors = await context.Doctors.Include(d => d.DoctorDetail).ToListAsync();
            int dIndex = 1;
            foreach (var d in allDoctors)
            {
                d.ProfileImage = $"/images/doctors/doctor-{((dIndex - 1) % 24) + 1}.svg";
                dIndex++;
            }
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during database seeding.");
            throw;
        }
    }

    private static string GenerateProductSlug(string title)
    {
        var s = title.ToLowerInvariant();
        s = System.Text.RegularExpressions.Regex.Replace(s, @"[^a-z0-9\s-]", "");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"[\s-]+", "-").Trim('-');
        return s;
    }

    public static async Task SeedDoctorsAsync(
        AyurvedaDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        bool isDev = true,
        string? devDoctorCredential = null)
    {
        var categories = await context.Categories.ToListAsync();
        var defaultCat = categories.FirstOrDefault() ?? throw new InvalidOperationException("Categories not seeded");

        var doctorDefs = new (string Name, string Email, string Qualification, int Experience, string Gender, string Languages, decimal Fee, double Rating, int Reviews, string Specialization, bool IsOnline, string Bio, string RegNo, string Council, string Address)[]
        {
            ("Dr. Aarav Sharma", "dr.aarav.sharma@thegreatayurveda.com", "BAMS, MD (Kayachikitsa)", 16, "Male", "Hindi, English", 599m, 4.9, 142, "Digestive Care", true,
             "Senior Vaidya specializing in chronic gut dysbiosis, Grahani, and hyperacidity. Over 16 years restoring digestive Agni through classical Bhaishajya Ratnavali protocols.",
             "DEMO-REG-8941", "Delhi Bharatiya Chikitsa Parishad", "Shree Dhanvantari Clinic, Defence Colony, New Delhi"),

            ("Dr. Ananya Nambiar", "dr.ananya.nambiar@thegreatayurveda.com", "BAMS, MD (Prasuti & Stri Roga)", 14, "Female", "Malayalam, English, Hindi", 699m, 4.95, 218, "Women's Wellness", true,
             "Renowned Kerala Ayurvedic gynecological specialist focusing on PCOS reversal, prenatal Garbhini charya, and natural menopause transition using classical Rasayanas.",
             "DEMO-REG-4102", "Travancore-Cochin Medical Council", "Nambiar Ayurveda Nilayam, Kochi, Kerala"),

            ("Dr. Vikramaditya Joshi", "dr.vikram.joshi@thegreatayurveda.com", "BAMS, Ph.D (Panchakarma)", 24, "Male", "Marathi, Hindi, English, Sanskrit", 1199m, 5.0, 310, "Panchakarma & Therapy Kits", false,
             "Master Panchakarma physician trained in classical Ashtanga Hridaya. Advises on customized Shodhana therapies, Vamana, Virechana, and Basti regimens for deep toxin detox.",
             "DEMO-REG-1039", "Maharashtra Council of Indian Medicine", "Vaidya Joshi Ayurveda Peeth, Sadashiv Peth, Pune"),

            ("Dr. Meera Deshmukh", "dr.meera.deshmukh@thegreatayurveda.com", "BAMS, Fellow in Ayurvedic Cosmetology", 8, "Female", "Marathi, Hindi, English", 449m, 4.8, 88, "Skin Care", true,
             "Specialist in holistic dermatological wellness. Expert in herbal management of stubborn psoriasis, eczema, acne vulgaris, and hyperpigmentation with blood-purifying Raktashodhaka churnas.",
             "DEMO-REG-6620", "Maharashtra Council of Indian Medicine", "AyurSkin Holistic Care, Bandra West, Mumbai"),

            ("Dr. Rajeshwari Iyer", "dr.rajeshwari.iyer@thegreatayurveda.com", "BAMS, MD (Kaumarbhritya - Pediatrics)", 18, "Female", "Tamil, English, Hindi", 799m, 4.9, 175, "Immunity & Rasayana", false,
             "Ayurvedic pediatrician with extensive clinical expertise in Suvarnaprashana, recurring childhood respiratory allergies, gut immunity, and holistic pediatric nourishment.",
             "DEMO-REG-3391", "Tamil Nadu Board of Indian Medicine", "Sri Shankara Ayurvedic Child Clinic, Mylapore, Chennai"),

            ("Dr. Harish Chandra Shastri", "dr.harish.shastri@thegreatayurveda.com", "BAMS, Senior Vaidya", 28, "Male", "Hindi, Sanskrit", 999m, 4.95, 260, "Joint & Muscle Care", false,
             "Traditional Vaidya lineage practitioner with 28 years mastering Sandhivata (osteoarthritis), cervical spondylosis, and sciatica through tailored herbal tailas and Janu Basti guidance.",
             "DEMO-REG-0872", "Board of Ayurvedic and Unani Systems of Medicine, UP", "Shastri Ayurvedic Chikitsalaya, Assi Ghat, Varanasi"),

            ("Dr. Tanvi Bhatia", "dr.tanvi.bhatia@thegreatayurveda.com", "BAMS, Sleep Wellness Counselor", 6, "Female", "Hindi, Punjabi, English", 399m, 4.75, 64, "Stress, Sleep & Brain", true,
             "Integrative Ayurvedic consultant dedicated to treating chronic insomnia, burnout, Vata-induced anxiety, and mental fatigue using Medhya botanicals and Shirodhara self-care rituals.",
             "DEMO-REG-9012", "Punjab Ayurvedic Council", "MindVeda Holistic Wellness, Sector 35, Chandigarh"),

            ("Dr. Devendra Hegde", "dr.devendra.hegde@thegreatayurveda.com", "BAMS, MD (Dravyaguna Vijnana)", 15, "Male", "Kannada, English, Hindi", 649m, 4.85, 130, "Herbs & Raw Powders", false,
             "Authority on botanical taxonomy and classical Dravya formulation. Advises patients on correct single-herb decoctions, therapeutic dosages, and wildcrafted herb synergy.",
             "DEMO-REG-5521", "Karnataka Ayurvedic and Unani Practitioners Board", "Hegde Herbal Heritage, Malleshwaram, Bengaluru"),

            ("Dr. Suniti Mukherjee", "dr.suniti.mukherjee@thegreatayurveda.com", "BAMS, MD (Kayachikitsa)", 11, "Female", "Bengali, English, Hindi", 549m, 4.8, 112, "Diabetes & Metabolism Support", true,
             "Focused on reversing insulin resistance and metabolic sluggishness through Agni-kindling herbs, Karela-Jamun extracts, and customized Ahara charya meal planning.",
             "DEMO-REG-7821", "Paschim Banga Ayurved Parishad", "Prana Health Center, Salt Lake, Kolkata"),

            ("Dr. Pradeep Varma", "dr.pradeep.varma@thegreatayurveda.com", "BAMS, MS (Ayur - Shalakya Tantra)", 19, "Male", "Malayalam, Tamil, English", 849m, 4.9, 158, "Panchakarma & Therapy Kits", false,
             "ENT and Netra Roga expert specializing in chronic sinusitis, allergic rhinitis, Nasya therapy, computer vision syndrome, and Tarpana ocular rejuvenation.",
             "DEMO-REG-2844", "Travancore-Cochin Medical Council", "Varma Shalakya Clinic, Thiruvananthapuram"),

            ("Dr. Shalini Kulkarni", "dr.shalini.kulkarni@thegreatayurveda.com", "BAMS, Trichology Specialist", 5, "Female", "Marathi, Hindi, English", 349m, 4.7, 52, "Hair Care", true,
             "Dedicated Ayurvedic trichologist addressing severe hair fall (Khalitya), premature graying (Palitya), and scalp microbiome balance with classical Bhringraj and Amla regimens.",
             "DEMO-REG-8834", "Maharashtra Council of Indian Medicine", "KeshVeda Clinic, Shivaji Nagar, Pune"),

            ("Dr. Balram Upadhyay", "dr.balram.upadhyay@thegreatayurveda.com", "BAMS, MD (Ayurveda)", 22, "Male", "Hindi, Bhojpuri, English", 899m, 4.9, 194, "Immunity & Rasayana", false,
             "Senior respiratory Ayurvedic clinician specializing in Shwasa-Kasa (bronchial asthma, chronic bronchitis), post-viral recovery, and long-term lung tissue strengthening.",
             "DEMO-REG-1922", "Board of Ayurvedic and Unani Systems of Medicine, UP", "AyurShwasa Kendra, Gomti Nagar, Lucknow"),

            ("Dr. Kavita Patel", "dr.kavita.patel@thegreatayurveda.com", "BAMS", 9, "Female", "Gujarati, Hindi, English", 499m, 4.8, 96, "Digestive Care", true,
             "Compassionate Vaidya specializing in IBS, chronic constipation, Agnimandya, and dietary re-education based on classical Ashtavidha Pariksha diagnosis.",
             "DEMO-REG-4910", "Gujarat Board of Ayurvedic and Unani Systems of Medicine", "Sattva Ayurvedic Care, Navrangpura, Ahmedabad"),

            ("Dr. Somnath Sen", "dr.somnath.sen@thegreatayurveda.com", "BAMS, MD (Rasashastra)", 20, "Male", "Bengali, Hindi, English", 950m, 4.95, 230, "Honey, Ghee & Natural Foods", false,
             "Authority on traditional pharmaceutics, medicinal clarified butters (Ghrithas), and classical Rasayanas for longevity, cellular repair, and anti-aging Ojas replenishment.",
             "DEMO-REG-3319", "Paschim Banga Ayurved Parishad", "Dhanwantari Rasashala, Ballygunge, Kolkata"),

            ("Dr. Priyamvada Rao", "dr.priyamvada.rao@thegreatayurveda.com", "BAMS", 7, "Female", "Telugu, English, Hindi", 449m, 4.75, 78, "Women's Wellness", true,
             "Holistic women's health practitioner specializing in hormonal acne, dysmenorrhea, postpartum rejuvenating care, and gentle Shatavari formulations.",
             "DEMO-REG-5108", "Andhra Pradesh Board of Ayurveda", "MatruVeda Center, Banjara Hills, Hyderabad"),

            ("Dr. Madhavan Pillai", "dr.madhavan.pillai@thegreatayurveda.com", "BAMS, Marma Master", 26, "Male", "Malayalam, English, Sanskrit", 1250m, 5.0, 290, "Joint & Muscle Care", false,
             "Legendary Kerala Marma therapist with 26 years correcting chronic back pain, slipped discs, sciatica, and sports injuries without surgical intervention.",
             "DEMO-REG-1182", "Travancore-Cochin Medical Council", "Pillai Marma Gurukulam, Palakkad, Kerala"),

            ("Dr. Alok Kumar Tiwari", "dr.alok.tiwari@thegreatayurveda.com", "BAMS, MD (Manasa Roga)", 12, "Male", "Hindi, English", 599m, 4.85, 140, "Stress, Sleep & Brain", true,
             "Mind-body physician harmonizing mental clarity and neurological calm through Satvavajaya Chikitsa, Medhya rasayanas, and lifestyle circadian synchronization.",
             "DEMO-REG-6619", "Delhi Bharatiya Chikitsa Parishad", "Chetana Ayurvedic Center, Rohini, Delhi"),

            ("Dr. Rituja Chawla", "dr.rituja.chawla@thegreatayurveda.com", "BAMS, MD (Twak Roga)", 10, "Female", "Hindi, English", 650m, 4.9, 125, "Skin Care", true,
             "Expert on allergic dermatitis, urticaria (Sheetapitta), Vitiligo supportive protocols, and luminous skin glow using Kumkumadi Tailam and Manjistha root.",
             "DEMO-REG-8120", "Delhi Bharatiya Chikitsa Parishad", "ShubhAyur Skin Clinic, South Extension, New Delhi"),

            ("Dr. Eknath Gaikwad", "dr.eknath.gaikwad@thegreatayurveda.com", "BAMS", 17, "Male", "Marathi, Hindi, English", 700m, 4.85, 160, "Diabetes & Metabolism Support", false,
             "Specializing in metabolic syndrome, fatty liver grade 1/2 reversal support, and lipid balance using classical Guggulu and Arogyavardhini protocols.",
             "DEMO-REG-4412", "Maharashtra Council of Indian Medicine", "Gaikwad Arogya Bhavan, Nashik"),

            ("Dr. Jayashree Sundaram", "dr.jayashree.sundaram@thegreatayurveda.com", "BAMS, Yoga Acharya", 13, "Female", "Tamil, English, Hindi", 620m, 4.8, 115, "Yoga & Wellness", true,
             "Integrates classical therapeutic Yoga asanas, Pranayama breathing, and herbal medicine to cure postural disorders, chronic migraines, and hormonal imbalances.",
             "DEMO-REG-5901", "Tamil Nadu Board of Indian Medicine", "AyurYoga Sanjeevani, Coimbatore"),

            ("Dr. Gaurav Aggarwal", "dr.gaurav.aggarwal@thegreatayurveda.com", "BAMS", 4, "Male", "Hindi, English", 299m, 4.65, 38, "Hair Care", true,
             "Young dynamic Vaidya focused on scalp health, modern lifestyle hair loss, dandruff, and stress-related alopecia with affordable consultation options.",
             "DEMO-REG-9410", "Haryana Board of Ayurvedic Systems", "KeshRoot Health, Gurugram"),

            ("Dr. Vasantha Kumari", "dr.vasantha.kumari@thegreatayurveda.com", "BAMS, MD (Jara Chikitsa)", 25, "Female", "Telugu, Kannada, English", 1100m, 4.95, 245, "Immunity & Rasayana", false,
             "Senior geriatric physician helping elders maintain vitality, memory retention, joint mobility, digestive warmth, and peaceful sleep in their golden years.",
             "DEMO-REG-2209", "Karnataka Ayurvedic and Unani Practitioners Board", "Sanjeevani Elder Care Ayurveda, Jayanagar, Bengaluru"),

            ("Dr. Nikhilesh Roy", "dr.nikhilesh.roy@thegreatayurveda.com", "BAMS", 3, "Male", "Hindi, Bengali, English", 320m, 4.6, 24, "Digestive Care", true,
             "Approachable general Ayurvedic consultant providing fast consultations for acute indigestion, travel sickness, bloating, and simple herbal remedies.",
             "DEMO-REG-1180", "Paschim Banga Ayurved Parishad", "Arogya Niketan, Howrah"),

            ("Dr. Sarojini Nair", "dr.sarojini.nair@thegreatayurveda.com", "BAMS, MD (Panchakarma), Gold Medalist", 30, "Female", "Malayalam, English, Hindi, Sanskrit", 1499m, 5.0, 380, "Panchakarma & Therapy Kits", false,
             "Distinguished Ayurvedic scholar and master clinician with 30 years experience heading premier Ayurvedic hospitals. Renowned worldwide for complex case management.",
             "DEMO-REG-0014", "Travancore-Cochin Medical Council", "Nair Classical Ayurvedic Hospital, Kottayam, Kerala")
        };

        var morningSlotsJson = "[\"09:00 AM\", \"09:30 AM\", \"10:00 AM\", \"10:30 AM\", \"11:00 AM\", \"11:30 AM\"]";
        var afternoonSlotsJson = "[\"02:00 PM\", \"02:30 PM\", \"03:00 PM\", \"03:30 PM\", \"04:00 PM\"]";
        var eveningSlotsJson = "[\"05:30 PM\", \"06:00 PM\", \"06:30 PM\", \"07:00 PM\", \"07:30 PM\"]";

        int doctorCounter = 1;
        var docPwd = isDev 
            ? (devDoctorCredential ?? "DevDemoPass_2026!#") 
            : Guid.NewGuid().ToString("N") + "!Aa9#" + Guid.NewGuid().ToString("N");
        foreach (var def in doctorDefs)
        {
            var doctorContact = $"+91 98765 {43210 + doctorCounter:D5}";
            doctorCounter++;

            // 1. Ensure ApplicationUser exists
            var user = await userManager.FindByEmailAsync(def.Email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = def.Email,
                    Email = def.Email,
                    FullName = def.Name,
                    ContactNumber = doctorContact,
                    Gender = def.Gender,
                    EmailConfirmed = true,
                    IsActive = true,
                    LockoutEnabled = !isDev,
                    LockoutEnd = !isDev ? DateTimeOffset.MaxValue : null,
                    CreatedAt = DateTime.UtcNow
                };
                var userRes = await userManager.CreateAsync(user, docPwd);
                if (userRes.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, UserRole.Doctor.ToString());
                }
            }
            else
            {
                if (!await userManager.IsInRoleAsync(user, UserRole.Doctor.ToString()))
                {
                    await userManager.AddToRoleAsync(user, UserRole.Doctor.ToString());
                }
            }

            // 2. Ensure Doctor entity & DoctorDetail
            var cat = categories.FirstOrDefault(c => c.Name.Equals(def.Specialization, StringComparison.OrdinalIgnoreCase)) ?? defaultCat;
            var existingDoctor = await context.Doctors.Include(d => d.DoctorDetail).FirstOrDefaultAsync(d => d.Email == def.Email);

            if (existingDoctor != null)
            {
                existingDoctor.ApplicationUserId = user.Id;
                existingDoctor.ProfileImage = $"/images/doctors/doctor-{((doctorCounter - 2) % 24) + 1}.webp";
                if (existingDoctor.DoctorDetail != null)
                {
                    existingDoctor.DoctorDetail.Status = 1; // Approved
                }
                else
                {
                    existingDoctor.DoctorDetail = new DoctorDetail
                    {
                        DoctorId = existingDoctor.Id,
                        Qualification = def.Qualification,
                        Experience = def.Experience,
                        Gender = def.Gender,
                        Language = def.Languages,
                        ConsultationFee = def.Fee,
                        Rating = def.Rating,
                        ReviewCount = def.Reviews,
                        IsOnline = def.IsOnline,
                        IsDemoData = true,
                        Bio = def.Bio,
                        RegistrationNumber = def.RegNo,
                        RegistrationCouncil = def.Council,
                        ClinicAddress = def.Address,
                        SpecializationId = cat.Id,
                        Status = 1,
                        MorningTimesJson = morningSlotsJson,
                        AfternoonTimesJson = afternoonSlotsJson,
                        EveningTimesJson = eveningSlotsJson,
                        CreatedAt = DateTime.UtcNow
                    };
                }
            }
            else
            {
                var doctor = new Doctor
                {
                    DoctorName = def.Name,
                    Email = def.Email,
                    ContactNumber = doctorContact,
                    ProfileImage = $"/images/doctors/doctor-{((doctorCounter - 2) % 24) + 1}.webp",
                    Role = "Doctor",
                    ApplicationUserId = user.Id,
                    CreatedAt = DateTime.UtcNow
                };

                var detail = new DoctorDetail
                {
                    Doctor = doctor,
                    Qualification = def.Qualification,
                    Experience = def.Experience,
                    Gender = def.Gender,
                    Language = def.Languages,
                    ConsultationFee = def.Fee,
                    Rating = def.Rating,
                    ReviewCount = def.Reviews,
                    IsOnline = def.IsOnline,
                    IsDemoData = true,
                    Bio = def.Bio,
                    RegistrationNumber = def.RegNo,
                    RegistrationCouncil = def.Council,
                    ClinicAddress = def.Address,
                    SpecializationId = cat.Id,
                    Status = 1, // Approved
                    MorningTimesJson = morningSlotsJson,
                    AfternoonTimesJson = afternoonSlotsJson,
                    EveningTimesJson = eveningSlotsJson,
                    CreatedAt = DateTime.UtcNow
                };

                doctor.DoctorDetail = detail;
                context.Doctors.Add(doctor);
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded 24 authentic fictional Ayurvedic Vaidyas with full credentials, slot schedules, and login accounts.");

        // Seed sample completed appointment with prescription & verified review
        var firstDoc = await context.Doctors.Include(d => d.DoctorDetail).FirstOrDefaultAsync();
        var patientUser = await context.Users.FirstOrDefaultAsync(u => u.Email != "admin@thegreatayurveda.com");
        if (firstDoc != null && patientUser != null && !await context.Appointments.AnyAsync())
        {
            var firstProduct = await context.Products.FirstOrDefaultAsync();
            var prescribedJson = firstProduct != null 
                ? $"[{{\"ProductId\":{firstProduct.Id},\"ProductTitle\":\"{firstProduct.Title}\",\"Dosage\":\"1 tsp twice daily with warm water after meals\",\"Duration\":\"30 Days\",\"Price\":{firstProduct.Price},\"ImageUrl\":\"{firstProduct.ImageUrl}\"}}]"
                : "[]";

            var pastAppt = new Appointment
            {
                DoctorId = firstDoc.Id,
                UserId = patientUser.Id,
                AppointmentDate = DateTime.Today.AddDays(-3),
                StartUtc = DateTime.UtcNow.AddDays(-3),
                TimeSlot = "10:00 AM",
                PatientName = patientUser.FullName,
                PatientContact = "+91 98765 00000",
                PatientAge = 34,
                PatientGender = "Male",
                HealthConcern = "Chronic morning bloating, sluggish digestion, and fatigue after meals",
                ConsultationType = ConsultationType.Video,
                ConsultationFee = firstDoc.DoctorDetail!.ConsultationFee,
                Status = "Completed",
                MeetingRoomId = "https://meet.jit.si/TheGreatAyurveda_DemoRoom_01",
                PrescriptionNotes = "Mild Mandagni with aggravated Samana Vata. Advised to avoid cold dairy, practice CCF tea infusions, and take prescribed classical formulations regularly.",
                DietaryAdvice = "Sip warm water with cumin seeds throughout the day. Take light dinner by 7:30 PM. Avoid raw salads at night.",
                DoshaDiagnosis = "Vata-Kapha Agnimandya",
                PrescriptionProductsJson = prescribedJson,
                FollowUpDateUtc = DateTime.UtcNow.AddDays(14),
                CallDurationSeconds = 1240,
                JoinedAtUtc = DateTime.UtcNow.AddDays(-3),
                LeftAtUtc = DateTime.UtcNow.AddDays(-3).AddMinutes(21)
            };

            var review = new DoctorReview
            {
                DoctorId = firstDoc.Id,
                UserId = patientUser.Id,
                Appointment = pastAppt,
                Rating = 5,
                ReviewText = "Exceptionally insightful consultation! Dr. Sharma explained the root cause of my digestive imbalance thoroughly and the dietary advice started helping within 3 days.",
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            };

            context.Appointments.Add(pastAppt);
            context.DoctorReviews.Add(review);

            // Upcoming appointment
            var upcomingAppt = new Appointment
            {
                DoctorId = firstDoc.Id,
                UserId = patientUser.Id,
                AppointmentDate = DateTime.Today.AddDays(1),
                StartUtc = DateTime.UtcNow.AddDays(1),
                TimeSlot = "11:00 AM",
                PatientName = patientUser.FullName,
                PatientContact = "+91 98765 00000",
                PatientAge = 34,
                PatientGender = "Male",
                HealthConcern = "Follow-up consultation for digestion progress review",
                ConsultationType = ConsultationType.Video,
                ConsultationFee = firstDoc.DoctorDetail.ConsultationFee,
                Status = "Confirmed",
                MeetingRoomId = "https://meet.jit.si/TheGreatAyurveda_DemoRoom_02"
            };
            context.Appointments.Add(upcomingAppt);

            await context.SaveChangesAsync();
            logger.LogInformation("Seeded sample completed appointment with digital prescription, verified review, and upcoming appointment.");
        }
    }


    public class ProductJsonItem
    {
        public string Title { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string SubcategoryName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Mrp { get; set; }
        public int Stock { get; set; }
        public double Rating { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public string TraditionallyUsedFor { get; set; } = string.Empty;
        public string Ingredients { get; set; } = string.Empty;
        public string HowToUse { get; set; } = string.Empty;
        public string Precautions { get; set; } = string.Empty;
        public bool IsPrescriptionRequired { get; set; }
        public List<ProductVariantJsonItem> Variants { get; set; } = new();
        public List<ProductReviewJsonItem> Reviews { get; set; } = new();
        public List<ProductFaqJsonItem> Faqs { get; set; } = new();
    }

    public class ProductVariantJsonItem
    {
        public string Title { get; set; } = string.Empty;
        public string SizeOrPack { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Mrp { get; set; }
        public int Stock { get; set; }
    }

    public class ProductReviewJsonItem
    {
        public int Rating { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Helpful { get; set; }
        public int DaysAgo { get; set; }
    }

    public class ProductFaqJsonItem
    {
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public string AskedBy { get; set; } = string.Empty;
    }

    private class ProductSeedDto
    {
        public string Title { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string SubcategoryName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Mrp { get; set; }
        public int Stock { get; set; }
        public double Rating { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public string TraditionallyUsedFor { get; set; } = string.Empty;
        public string Ingredients { get; set; } = string.Empty;
        public string HowToUse { get; set; } = string.Empty;
        public string Precautions { get; set; } = string.Empty;
        public bool IsPrescriptionRequired { get; set; }
        public List<(string Title, string SizeOrPack, decimal Price, decimal Mrp, int Stock)> Variants { get; set; } = new();
        public List<(int Rating, string Text, int Helpful, int DaysAgo)> Reviews { get; set; } = new();
        public List<(string Question, string Answer, string AskedBy)> Faqs { get; set; } = new();
    }

    private static List<ProductSeedDto> GetSeedCatalogData()
    {
        return new List<ProductSeedDto>
        {
            // 1. Immunity & Rasayana -> Chyawanprash
            new()
            {
                Title = "Organic Amla Chyawanprash Special",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Immunity & Rasayana",
                SubcategoryName = "Chyawanprash",
                Description = "Classical Rasayana crafted with 40+ wildcrafted herbs, fresh forest Amla, raw honey, and Vedic A2 cow ghee. Restores Ojas and seasonal immunity.",
                Price = 450m, Mrp = 550m, Stock = 65, Rating = 4.9,
                ImageUrl = "/images/products/chyawanprash.svg", Sku = "TGA-IMM-001",
                Tags = "immunity, ojas, rasayana, chyawanprash, organic",
                TraditionallyUsedFor = "Seasonal Immunity, Respiratory Strength, Ojas Vitality",
                Ingredients = "Fresh Organic Amla, Ashwagandha, Shatavari, Pipali, Cardamom, Raw Forest Honey, A2 Cow Ghee",
                HowToUse = "1-2 teaspoons twice daily with warm milk or lukewarm water on an empty stomach.",
                Precautions = "Contains honey and jaggery; consult doctor if diabetic.",
                Variants = new() { ("500g Jar", "500g", 450m, 550m, 40), ("1kg Family Pack", "1kg", 820m, 999m, 25) },
                Reviews = new() { (5, "Authentic taste without artificial sweetness. Energy levels improved significantly.", 14, 12), (5, "Classic Ayurvedic texture with real amla goodness.", 8, 25) },
                Faqs = new() { ("Can children consume this Chyawanprash?", "Yes, children above 3 years can take half a teaspoon daily with warm milk.", "Ramesh K.") }
            },
            new()
            {
                Title = "Sugar-Free Gold Chyawanprash",
                Brand = "VaidyaRoots",
                CategoryName = "Immunity & Rasayana",
                SubcategoryName = "Chyawanprash",
                Description = "Specially formulated for blood sugar conscious individuals, infused with Swarna Bhasma and pure herbs without refined sugars or jaggery.",
                Price = 690m, Mrp = 850m, Stock = 42, Rating = 4.8,
                ImageUrl = "/images/products/chyawanprash.svg", Sku = "VR-IMM-002",
                Tags = "sugarfree, gold, chyawanprash, immunity",
                TraditionallyUsedFor = "Diabetic-Friendly Vitality, Natural Cellular Defense",
                Ingredients = "Amla Pulp, Swarna Bhasma, Gudmar, Jamun seed extract, Ashwagandha, Sesame Oil",
                HowToUse = "1 teaspoon in the morning with lukewarm water.",
                Precautions = "Safe for diabetic adults; follow physician's dietary instructions.",
                Variants = new() { ("500g Jar", "500g", 690m, 850m, 42) },
                Reviews = new() { (5, "Zero spike in blood sugar, very rejuvenating.", 9, 8) }
            },

            // 1. Immunity & Rasayana -> Herbal Tonics
            new()
            {
                Title = "Drakshasava Classical Herbal Elixir",
                Brand = "AyurVeda Sanjeevani",
                CategoryName = "Immunity & Rasayana",
                SubcategoryName = "Herbal Tonics",
                Description = "Naturally fermented herbal asava prepared from black raisins (Draksha). Traditionally revitalizes digestive fire and overcomes general debility.",
                Price = 280m, Mrp = 340m, Stock = 38, Rating = 4.7,
                ImageUrl = "/images/products/triphala.svg", Sku = "AS-IMM-003",
                Tags = "drakshasava, asava, digestive tonic, fatigue",
                TraditionallyUsedFor = "General Debility, Chronic Fatigue, Blood Purification",
                Ingredients = "Draksha (Raisins), Dhataki flowers, Cinnamon, Cardamom, Long Pepper, Jaggery",
                HowToUse = "15-20 ml twice daily with an equal quantity of water after meals.",
                Precautions = "Natural self-generated alcohol up to 8%. Avoid on empty stomach.",
                Variants = new() { ("450ml Bottle", "450ml", 280m, 340m, 38) }
            },
            new()
            {
                Title = "Amritaprasha Rasayana Revitalizer",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Immunity & Rasayana",
                SubcategoryName = "Herbal Tonics",
                Description = "Potent classical restorative avaleha described in Ashtanga Hridaya. Rebuilds deep tissues, promotes stamina and reproductive vitality.",
                Price = 580m, Mrp = 720m, Stock = 18, Rating = 4.9,
                ImageUrl = "/images/products/chyawanprash.svg", Sku = "TGA-IMM-004",
                Tags = "rasayana, vitality, stamina, tissue strength",
                TraditionallyUsedFor = "Post-Illness Convalescence, Deep Tissue Nourishment",
                Ingredients = "Ghee, Honey, Ashwagandha, Shatavari, Vidari, Gokshura, Pippali, Sugar candy",
                HowToUse = "1 teaspoon twice daily followed by warm cow milk.",
                Precautions = "Rich formulation; avoid in high ama (indigestion) conditions."
            },

            // 1. Immunity & Rasayana -> Giloy & Tulsi
            new()
            {
                Title = "Pure Giloy Ghanvati Tablets (500mg)",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Immunity & Rasayana",
                SubcategoryName = "Giloy & Tulsi",
                Description = "Concentrated aqueous extract of Tinospora cordifolia (Amrita). Clarifies Pitta, strengthens liver metabolism and supports leukocyte count.",
                Price = 180m, Mrp = 220m, Stock = 110, Rating = 4.8,
                ImageUrl = "/images/products/triphala.svg", Sku = "TGA-IMM-005",
                Tags = "giloy, amrita, fever, immunity, tablets",
                TraditionallyUsedFor = "Recurrent Fevers, Liver Detoxification, Natural Platelet Support",
                Ingredients = "Pure Giloy Stem Extract (Tinospora cordifolia 500mg)",
                HowToUse = "1-2 tablets twice daily after meals with lukewarm water.",
                Precautions = "Do not exceed recommended dose.",
                Variants = new() { ("60 Tablets", "60 Tabs", 180m, 220m, 70), ("120 Tablets Value Pack", "120 Tabs", 320m, 400m, 40) }
            },
            new()
            {
                Title = "Panch Tulsi Pure Herbal Drops",
                Brand = "Dhanwantari Naturals",
                CategoryName = "Immunity & Rasayana",
                SubcategoryName = "Giloy & Tulsi",
                Description = "Steam-distilled blend of 5 sacred Holy Basil varieties: Rama, Shyama, Krishna, Van, and Shukla Tulsi. Enhances respiratory prana.",
                Price = 195m, Mrp = 250m, Stock = 85, Rating = 4.7,
                ImageUrl = "/images/products/hair-oil.svg", Sku = "DN-IMM-006",
                Tags = "tulsi, drops, respiratory, immunity",
                TraditionallyUsedFor = "Throat Comfort, Cough Relief, Environmental Defense",
                Ingredients = "Ocimum sanctum, Ocimum gratissimum, Ocimum canum, Ocimum basilicum, Ocimum kilimandscharicum",
                HowToUse = "2-3 drops in a glass of warm water or green tea twice daily.",
                Precautions = "Store in a cool dry place away from sunlight."
            },

            // 2. Digestive Care -> Churnas
            new()
            {
                Title = "Triphala Churna Digestive Powder",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Digestive Care",
                SubcategoryName = "Churnas",
                Description = "Traditional tri-doshic blend of Haritaki, Bibhitaki, and Amalaki. Gently supports digestion, colon tone, and natural detoxification.",
                Price = 220m, Mrp = 275m, Stock = 90, Rating = 4.8,
                ImageUrl = "/images/products/triphala.svg", Sku = "TGA-DIG-001",
                Tags = "triphala, churna, digestion, detox, colon",
                TraditionallyUsedFor = "Bowel Regularity, Colon Cleansing, Gentle Detox",
                Ingredients = "Terminalia chebula (Haritaki), Terminalia bellirica (Bibhitaki), Emblica officinalis (Amalaki)",
                HowToUse = "1 teaspoon before bedtime with warm water.",
                Precautions = "Avoid during acute diarrhea or severe dehydration.",
                Variants = new() { ("200g Pack", "200g", 220m, 275m, 60), ("500g Eco Pouch", "500g", 480m, 600m, 30) },
                Reviews = new() { (5, "The gold standard Triphala. Regularity restored in 3 days.", 22, 18) }
            },
            new()
            {
                Title = "Hingwashtak Churna Classical Agni Tonic",
                Brand = "VaidyaRoots",
                CategoryName = "Digestive Care",
                SubcategoryName = "Churnas",
                Description = "Authentic formulation of Asafoetida, ginger, black pepper, and cumin. Stimulates digestive fire (Deepana) and digests metabolic toxins (Pachana).",
                Price = 190m, Mrp = 230m, Stock = 45, Rating = 4.7,
                ImageUrl = "/images/products/triphala.svg", Sku = "VR-DIG-002",
                Tags = "hingwashtak, bloat, gas, agni, churna",
                TraditionallyUsedFor = "Gas, Abdominal Heaviness, Sluggish Digestion",
                Ingredients = "Hing (Shuddha Asafoetida), Sunthi, Maricha, Pippali, Ajwain, Cumin, Black Cumin, Rock Salt",
                HowToUse = "Half a teaspoon mixed with the first bite of food with ghee.",
                Precautions = "Contains rock salt; limit use in severe hypertension."
            },

            // 2. Digestive Care -> Tablets
            new()
            {
                Title = "Abhayarishta & Shankh Vati Digestive Combo",
                Brand = "AyurVeda Sanjeevani",
                CategoryName = "Digestive Care",
                SubcategoryName = "Tablets",
                Description = "Herbo-mineral classical tablets prepared with purified conch shell bhasma and digestive spices to relieve acid reflux and sour belching.",
                Price = 240m, Mrp = 299m, Stock = 50, Rating = 4.6,
                ImageUrl = "/images/products/triphala.svg", Sku = "AS-DIG-003",
                Tags = "shankh vati, acidity, reflux, digestive",
                TraditionallyUsedFor = "Hyperacidity, Indigestion, Stomach Discomfort",
                Ingredients = "Shankha Bhasma, Shuddha Vatsanabha, Trikatu, Pancha Lavana, Lemon juice bhavana",
                HowToUse = "1-2 tablets after meals with lukewarm water.",
                Precautions = "Contains processed mineral bhasma; follow doctor guidance for prolonged use."
            },
            new()
            {
                Title = "Avisipattikar Pitta Harmony Tablets",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Digestive Care",
                SubcategoryName = "Tablets",
                Description = "Classic cooling formulation described in Bhaishajya Ratnavali for neutralizing aggravated Pitta dosha in the upper GI tract.",
                Price = 210m, Mrp = 260m, Stock = 65, Rating = 4.8,
                ImageUrl = "/images/products/triphala.svg", Sku = "TGA-DIG-004",
                Tags = "avipattikar, pitta, acid relief, heartburn",
                TraditionallyUsedFor = "Pitta Balance, Heartburn, Bilious Vomiting",
                Ingredients = "Trikatu, Triphala, Musta, Vidanga, Cardamom, Tejpatra, Lavanga, Trivrit, Sharkara",
                HowToUse = "1-2 tablets before meals with cool or room temperature water.",
                Precautions = "Not suitable for individuals with loose bowels."
            },

            // 2. Digestive Care -> Herbal Teas
            new()
            {
                Title = "CCF Digestive Detox Tea (Cumin, Coriander, Fennel)",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Digestive Care",
                SubcategoryName = "Herbal Teas",
                Description = "The revered Ayurvedic tridoshic herbal infusion. Clarifies lymphatic stagnation, debloats, and kindles gentle Agni without heating.",
                Price = 275m, Mrp = 350m, Stock = 80, Rating = 4.9,
                ImageUrl = "/images/products/triphala.svg", Sku = "TGA-DIG-005",
                Tags = "ccf tea, detox, cumin, coriander, fennel",
                TraditionallyUsedFor = "Gentle Detox, Fluid Balance, Post-Meal Comfort",
                Ingredients = "Organic Cumin seeds, Coriander seeds, Sweet Fennel seeds",
                HowToUse = "Steep 1 tea bag or 1 tsp in boiling water for 5-7 minutes. Sip throughout the day.",
                Precautions = "Safe for daily family consumption.",
                Variants = new() { ("25 Tea Bags", "25 Bags", 275m, 350m, 50), ("50 Tea Bags Tin", "50 Bags", 495m, 620m, 30) }
            },
            new()
            {
                Title = "Brahmi Ginger Agni Tisane",
                Brand = "Himalayan Vana",
                CategoryName = "Digestive Care",
                SubcategoryName = "Herbal Teas",
                Description = "A warming blend of Himalayan dried ginger, whole Brahmi leaves, and sweet licorice root for post-meal digestive serenity.",
                Price = 310m, Mrp = 390m, Stock = 34, Rating = 4.7,
                ImageUrl = "/images/products/triphala.svg", Sku = "HV-DIG-006",
                Tags = "ginger tea, brahmi, agni, herbal tea",
                TraditionallyUsedFor = "Slow Metabolism, Post-Prandial Heaviness",
                Ingredients = "Wild Dry Ginger, Brahmi, Licorice, Cardamom, Holy Basil",
                HowToUse = "Boil 1 teaspoon in 250ml water for 3 minutes. Strain and enjoy warm.",
                Precautions = "Licorice is naturally sweet; no extra sweetener required."
            },

            // 3. Joint & Muscle Care -> Massage Oils
            new()
            {
                Title = "Mahanarayan Muscle & Joint Oil (Classical)",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Joint & Muscle Care",
                SubcategoryName = "Massage Oils",
                Description = "Authentic classical oil prepared with pure sesame oil base and 30+ botanical extracts including Dashamula, Ashwagandha, and Bala.",
                Price = 380m, Mrp = 450m, Stock = 55, Rating = 4.9,
                ImageUrl = "/images/products/mahanarayan.svg", Sku = "TGA-JNT-001",
                Tags = "mahanarayan, joint oil, massage, arthritis, vata",
                TraditionallyUsedFor = "Joint Flexibility, Back Comfort, Soothing Sore Muscles",
                Ingredients = "Sesame oil, Dashamula, Ashwagandha, Bala, Shatavari, Camphor, Cow milk",
                HowToUse = "Warm slightly in a water bath. Massage into affected areas with circular strokes.",
                Precautions = "External application only. Do not apply over broken skin.",
                Variants = new() { ("100ml Bottle", "100ml", 380m, 450m, 35), ("200ml Family Bottle", "200ml", 680m, 820m, 20) },
                Reviews = new() { (5, "Immediate soothing relief for my knee stiffness. Wonderful natural fragrance.", 18, 14) }
            },
            new()
            {
                Title = "Kottamchukkadi Thailam Swelling Relief Oil",
                Brand = "VaidyaRoots",
                CategoryName = "Joint & Muscle Care",
                SubcategoryName = "Massage Oils",
                Description = "Classical Kerala formulation with costus, dry ginger, and calamus in tamarind juice medium. Renowned for relieving inflammation and stiffness.",
                Price = 360m, Mrp = 425m, Stock = 38, Rating = 4.8,
                ImageUrl = "/images/products/mahanarayan.svg", Sku = "VR-JNT-002",
                Tags = "kottamchukkadi, kerala thailam, joint swelling, stiffness",
                TraditionallyUsedFor = "Localized Swelling, Morning Joint Stiffness, Vata-Kapha Disorders",
                Ingredients = "Kushta, Sunthi, Vacha, Shigru, Lasuna, Mustard oil, Sesame oil, Tamarind extract",
                HowToUse = "Apply warm oil generously over swollen joints and follow with warm fomentation.",
                Precautions = "For external use only. Discontinue if rash develops."
            },

            // 3. Joint & Muscle Care -> Balms
            new()
            {
                Title = "Rheuma-Cool Herbal Pain Balm",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Joint & Muscle Care",
                SubcategoryName = "Balms",
                Description = "Fast-acting topical balm with pure Wintergreen oil (Gandhapura), Eucalyptus, and Bhimseni Camphor in a beeswax base.",
                Price = 145m, Mrp = 180m, Stock = 120, Rating = 4.7,
                ImageUrl = "/images/products/mahanarayan.svg", Sku = "TGA-JNT-003",
                Tags = "balm, pain relief, headache, joint pain",
                TraditionallyUsedFor = "Headache, Neck Tension, Sudden Muscle Sprains",
                Ingredients = "Gandhapura taila (Wintergreen), Nilgiri taila (Eucalyptus), Karpura, Pudina Satva, Beeswax base",
                HowToUse = "Gently apply a thin layer over affected area and massage lightly.",
                Precautions = "Avoid contact with eyes and mucous membranes.",
                Variants = new() { ("25g Jar", "25g", 145m, 180m, 80), ("50g Value Pack", "50g", 260m, 320m, 40) }
            },
            new()
            {
                Title = "Vatagaja Deep-Penetrating Herbal Salve",
                Brand = "Dhanwantari Naturals",
                CategoryName = "Joint & Muscle Care",
                SubcategoryName = "Balms",
                Description = "Concentrated herbal salve infused with Shallaki (Boswellia), Guggulu, and Nirgundi oil to comfort deep joints.",
                Price = 220m, Mrp = 275m, Stock = 40, Rating = 4.6,
                ImageUrl = "/images/products/mahanarayan.svg", Sku = "DN-JNT-004",
                Tags = "salve, shallaki, boswellia, guggulu, balm",
                TraditionallyUsedFor = "Deep Joint Aches, Sciatic Comfort, Cartilage Support",
                Ingredients = "Boswellia serrata extract, Commiphora mukul, Nirgundi leaf oil, Kokum butter",
                HowToUse = "Rub gently into joints twice daily.",
                Precautions = "Wash hands after application."
            },

            // 3. Joint & Muscle Care -> Supplements
            new()
            {
                Title = "Shallaki & Yograj Guggulu Joint Tablets",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Joint & Muscle Care",
                SubcategoryName = "Supplements",
                Description = "Synergistic combination of pure Boswellia extract and purified classical Yograj Guggulu for cartilage integrity.",
                Price = 420m, Mrp = 520m, Stock = 60, Rating = 4.8,
                ImageUrl = "/images/products/ashwagandha.svg", Sku = "TGA-JNT-005",
                Tags = "shallaki, guggulu, joint supplements, mobility",
                TraditionallyUsedFor = "Joint Mobility, Connective Tissue Comfort, Vata Balance",
                Ingredients = "Pure Boswellia serrata (Shallaki extract), Shuddha Guggulu, Trikatu, Triphala",
                HowToUse = "1-2 tablets twice daily with warm water after meals.",
                Precautions = "Not recommended during pregnancy.",
                Variants = new() { ("60 Tablets", "60 Tabs", 420m, 520m, 60) }
            },
            new()
            {
                Title = "Asthishrinkhala Bone Density Capsules (Hadjod)",
                Brand = "VaidyaRoots",
                CategoryName = "Joint & Muscle Care",
                SubcategoryName = "Supplements",
                Description = "Pure Cissus quadrangularis extract, traditionally celebrated as 'Asthisamharaka' (bone-knit) to support bone mineral density.",
                Price = 380m, Mrp = 460m, Stock = 45, Rating = 4.7,
                ImageUrl = "/images/products/ashwagandha.svg", Sku = "VR-JNT-006",
                Tags = "hadjod, cissus, bone density, fractures, joints",
                TraditionallyUsedFor = "Bone Mineralization, Tendon Recovery, Fracture Healing Support",
                Ingredients = "Pure Hadjod (Cissus quadrangularis) stem extract 500mg",
                HowToUse = "1 capsule twice daily with warm milk.",
                Precautions = "Safe for long term use under clinical supervision."
            },

            // 4. Skin Care -> Face Care
            new()
            {
                Title = "Kumkumadi Tailam Miraculous Radiance Oil",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Skin Care",
                SubcategoryName = "Face Care",
                Description = "Authentic 26-herb formulation with pure Kashmiri Saffron (Kumkuma), sandalwood, and lotus pollen. Evens skin tone and restores youthful glow.",
                Price = 850m, Mrp = 1100m, Stock = 40, Rating = 4.9,
                ImageUrl = "/images/products/hair-oil.svg", Sku = "TGA-SKN-001",
                Tags = "kumkumadi, saffron, face oil, glowing skin, anti-aging",
                TraditionallyUsedFor = "Skin Complexion, Dark Circles, Fine Lines, Natural Radiance",
                Ingredients = "Pure Kashmiri Saffron, Red Sandalwood, Manjistha, Yashtimadhu, Ushira, Sesame Oil, Goat milk",
                HowToUse = "Massage 3-4 drops onto clean, damp face before bedtime. Leave overnight.",
                Precautions = "For dry to normal skin; use 1-2 drops for oily skin.",
                Variants = new() { ("15ml Dropper", "15ml", 850m, 1100m, 25), ("30ml Luxury Pack", "30ml", 1550m, 1999m, 15) },
                Reviews = new() { (5, "Pure luxury. My hyperpigmentation faded dramatically within two weeks.", 29, 9) }
            },
            new()
            {
                Title = "Kasturi Manjal & Sandalwood Glow Elixir",
                Brand = "Himalayan Vana",
                CategoryName = "Skin Care",
                SubcategoryName = "Face Care",
                Description = "Aromatic non-staining wild turmeric (Kasturi Manjal) infused in cold-pressed sweet almond oil with genuine Mysore sandalwood extract.",
                Price = 520m, Mrp = 650m, Stock = 32, Rating = 4.7,
                ImageUrl = "/images/products/hair-oil.svg", Sku = "HV-SKN-002",
                Tags = "wild turmeric, kasturi manjal, sandalwood, glow, face serum",
                TraditionallyUsedFor = "Blemish Control, Skin Calming, Sun Exposure Care",
                Ingredients = "Curcuma aromatica (Kasturi Manjal), Santalum album oil, Sweet Almond oil, Jojoba oil",
                HowToUse = "Apply 2-3 drops to face and neck every morning.",
                Precautions = "Patch test before first use."
            },

            // 4. Skin Care -> Body Care
            new()
            {
                Title = "Nalpamaradi Thailam Skin Brightening Body Oil",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Skin Care",
                SubcategoryName = "Body Care",
                Description = "Classical Kerala formulation with the bark of 4 sacred Ficus trees, turmeric, and Indian gooseberry. Renowned as a pre-bridal body brightening treatment.",
                Price = 440m, Mrp = 550m, Stock = 50, Rating = 4.8,
                ImageUrl = "/images/products/hair-oil.svg", Sku = "TGA-SKN-003",
                Tags = "nalpamaradi, body oil, brightening, de-tan, ficus",
                TraditionallyUsedFor = "De-tanning, Skin Texture, Body Glow, Blemish Clearing",
                Ingredients = "Nalpamara (4 Ficus barks), Turmeric, Triphala, Manjistha, Sesame oil",
                HowToUse = "Apply all over body 30 minutes before bathing. Wash off with a gentle herbal ubtan.",
                Precautions = "May stain light-colored clothing due to natural turmeric."
            },
            new()
            {
                Title = "Satadhauta Ghritha 100-Times Washed Ghee Cream",
                Brand = "AyurVeda Sanjeevani",
                CategoryName = "Skin Care",
                SubcategoryName = "Body Care",
                Description = "A2 Gir cow ghee washed 100 times in a consecrated copper vessel with purified water. Transforms into a velvety cooling cream.",
                Price = 620m, Mrp = 750m, Stock = 28, Rating = 4.9,
                ImageUrl = "/images/products/chyawanprash.svg", Sku = "AS-SKN-004",
                Tags = "satadhauta ghritha, washed ghee, cooling cream, eczema, burns",
                TraditionallyUsedFor = "Deep Skin Hydration, Eczema Soothing, Sunburn Relief, Pitta Calming",
                Ingredients = "Vedic A2 Gir Cow Ghee, Copper-distilled purified water",
                HowToUse = "Smooth a small quantity over dry or sensitized skin areas.",
                Precautions = "Completely fragrance and chemical free. Keep refrigerated in hot weather."
            },

            // 4. Skin Care -> Herbal Soaps & Ubtans
            new()
            {
                Title = "Traditional Tejasvi Bridal Ubtan Cleanser",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Skin Care",
                SubcategoryName = "Herbal Soaps & Ubtans",
                Description = "Micro-milled exfoliating powder with gram flour, wild turmeric, sandalwood, orange peel, and rose petals. Replaces chemical body wash.",
                Price = 290m, Mrp = 360m, Stock = 70, Rating = 4.8,
                ImageUrl = "/images/products/triphala.svg", Sku = "TGA-SKN-005",
                Tags = "ubtan, natural cleanser, body scrub, turmeric, rose",
                TraditionallyUsedFor = "Dead Skin Exfoliation, Silky Softness, Tan Removal",
                Ingredients = "Besan, Kasturi Turmeric, Rose petal powder, Chandan, Vetiver, Orange peel, Neem",
                HowToUse = "Mix with raw milk or rose water to form a smooth paste. Massage over skin and rinse.",
                Precautions = "Store powder dry; use clean spoon."
            },
            new()
            {
                Title = "Neem & Tulsi Purifying Bathing Bar (Set of 3)",
                Brand = "Dhanwantari Naturals",
                CategoryName = "Skin Care",
                SubcategoryName = "Herbal Soaps & Ubtans",
                Description = "Cold-process handmade herbal soap with pure neem oil, crushed tulsi leaves, and coconut oil. Purifies skin without stripping natural lipids.",
                Price = 240m, Mrp = 300m, Stock = 85, Rating = 4.6,
                ImageUrl = "/images/products/triphala.svg", Sku = "DN-SKN-006",
                Tags = "neem soap, tulsi, natural soap, antibacterial",
                TraditionallyUsedFor = "Daily Hygiene, Acne Prone Skin, Skin Odor Control",
                Ingredients = "Pure Neem oil, Holy Basil extract, Virgin Coconut oil, Castor oil, Essential oils",
                HowToUse = "Lather between wet hands and apply over body during bath.",
                Precautions = "For external use only."
            },

            // 5. Hair Care -> Hair Oils
            new()
            {
                Title = "Kuntal Care Bhringraj Hair Wellness Oil",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Hair Care",
                SubcategoryName = "Hair Oils",
                Description = "Classical Kshirapak method preparation with fresh Bhringraj juice, Amla, and Brahmi boiled in cow milk and virgin sesame oil.",
                Price = 340m, Mrp = 400m, Stock = 65, Rating = 4.9,
                ImageUrl = "/images/products/hair-oil.svg", Sku = "TGA-HAR-001",
                Tags = "bhringraj oil, hair fall, keshya, premature greying",
                TraditionallyUsedFor = "Hair Root Strength, Scalp Cooling, Deep Sleep Induction",
                Ingredients = "Bhringraj juice, Amalaki, Brahmi, Gunja, Sesame oil, Cow milk",
                HowToUse = "Gently massage into scalp roots with fingertips. Leave for at least 1 hour or overnight before wash.",
                Precautions = "External application only.",
                Variants = new() { ("100ml Bottle", "100ml", 340m, 400m, 40), ("200ml Family Pack", "200ml", 590m, 720m, 25) }
            },
            new()
            {
                Title = "Neelibhringadi Ayurvedic Scalp Elixir",
                Brand = "VaidyaRoots",
                CategoryName = "Hair Care",
                SubcategoryName = "Hair Oils",
                Description = "Renowned classical Kerala formulation featuring Indigo (Neeli), Bhringraj, Indian gooseberry, and coconut milk for lustrous dark hair.",
                Price = 420m, Mrp = 520m, Stock = 40, Rating = 4.8,
                ImageUrl = "/images/products/hair-oil.svg", Sku = "VR-HAR-002",
                Tags = "neelibhringadi, kerala hair oil, dark hair, dandruff",
                TraditionallyUsedFor = "Premature Greying, Scalp Nourishment, Hair Density",
                Ingredients = "Neeli (Indigofera tinctoria), Bhringraj, Amla, Yashtimadhu, Coconut milk, Buffalo milk, Goat milk",
                HowToUse = "Part hair into sections and massage oil into scalp.",
                Precautions = "Natural cooling herb; avoid if suffering from acute head cold."
            },

            // 5. Hair Care -> Shampoos
            new()
            {
                Title = "Shikakai & Reetha Gentle Scalp Cleanser",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Hair Care",
                SubcategoryName = "Shampoos",
                Description = "Sulfate-free, pH-balanced hair wash brewed with boiled soapnuts (Reetha), Shikakai pods, Hibiscus flowers, and Fenugreek.",
                Price = 320m, Mrp = 399m, Stock = 55, Rating = 4.7,
                ImageUrl = "/images/products/hair-oil.svg", Sku = "TGA-HAR-003",
                Tags = "shikakai shampoo, reetha, sulfate free, herbal shampoo",
                TraditionallyUsedFor = "Mild Hair Cleansing, Scalp pH Balance, Natural Shine",
                Ingredients = "Acacia concinna (Shikakai), Sapindus mukorossi (Reetha), Hibiscus rosa-sinensis, Trigonella foenum-graecum",
                HowToUse = "Apply to wet scalp, massage gently to generate mild natural lather, and rinse thoroughly.",
                Precautions = "Mild natural lather; avoid contact with eyes.",
                Variants = new() { ("200ml Pump", "200ml", 320m, 399m, 35), ("400ml Value Size", "400ml", 560m, 699m, 20) }
            },
            new()
            {
                Title = "Methi & Onion Root Strengthener Hair Wash",
                Brand = "Himalayan Vana",
                CategoryName = "Hair Care",
                SubcategoryName = "Shampoos",
                Description = "Enriched with cold-pressed Red Onion seed oil and sprouted Fenugreek (Methi) to reduce excessive hair shedding.",
                Price = 360m, Mrp = 450m, Stock = 35, Rating = 4.6,
                ImageUrl = "/images/products/hair-oil.svg", Sku = "HV-HAR-004",
                Tags = "onion shampoo, methi, hair fall control, biotin",
                TraditionallyUsedFor = "Breakage Reduction, Root Fortification, Follicle Stimulation",
                Ingredients = "Allium cepa extract, Trigonella seed extract, Bhringraj, Aloe vera juice, Plant surfactants",
                HowToUse = "Massage into wet hair for 2 minutes and rinse.",
                Precautions = "Color safe; free from parabens and SLS."
            },

            // 5. Hair Care -> Herbal Powders
            new()
            {
                Title = "Organic Tri-Herb Hair Pack (Amla, Bhringraj, Shikakai)",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Hair Care",
                SubcategoryName = "Herbal Powders",
                Description = "Sun-dried and triple-sifted 100% pure Ayurvedic powders. Acts as a detoxifying scalp mud mask and natural conditioning pack.",
                Price = 240m, Mrp = 300m, Stock = 75, Rating = 4.8,
                ImageUrl = "/images/products/triphala.svg", Sku = "TGA-HAR-005",
                Tags = "hair pack, amla powder, shikakai, bhringraj, hair mask",
                TraditionallyUsedFor = "Scalp Detoxification, Hair Root Conditioning, Volume Boost",
                Ingredients = "Organic Emblica officinalis, Eclipta alba, Acacia concinna",
                HowToUse = "Mix 2 tablespoons with warm water or yogurt. Apply from roots to ends for 30 minutes, then rinse.",
                Precautions = "Perform skin test prior to full application."
            },
            new()
            {
                Title = "Pure Rajasthani Sojat Henna Powder",
                Brand = "Dhanwantari Naturals",
                CategoryName = "Hair Care",
                SubcategoryName = "Herbal Powders",
                Description = "Premium high-lawsone content natural henna from Sojat, Rajasthan. Coats hair cuticles in rich conditioning russet tones.",
                Price = 180m, Mrp = 225m, Stock = 60, Rating = 4.7,
                ImageUrl = "/images/products/triphala.svg", Sku = "DN-HAR-006",
                Tags = "henna, sojat mehendi, hair color, conditioning",
                TraditionallyUsedFor = "Natural Hair Coloring, Scalp Cooling, Cuticle Protection",
                Ingredients = "100% Pure Lawsonia inermis (Henna) leaf powder",
                HowToUse = "Soak in black tea water overnight in an iron vessel. Apply to clean hair for 2-3 hours.",
                Precautions = "Natural plant dye; wear protective gloves."
            },

            // 6. Stress, Sleep & Brain -> Ashwagandha
            new()
            {
                Title = "Pure Ashwagandha Root Extract Capsules (500mg)",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Stress, Sleep & Brain",
                SubcategoryName = "Ashwagandha",
                Description = "Pure Withania somnifera root extract. Calms cortisol, improves stress resilience, and deepens restful sleep.",
                Price = 490m, Mrp = 599m, Stock = 80, Rating = 4.9,
                ImageUrl = "/images/products/ashwagandha.svg", Sku = "TGA-STR-001",
                Tags = "ashwagandha, cortisol, stress, sleep, adaptogen",
                TraditionallyUsedFor = "Stress Resilience, Sound Sleep, Physical Endurance",
                Ingredients = "Pure Ashwagandha Root Extract 500mg",
                HowToUse = "1 capsule twice daily after meals with warm water or milk.",
                Precautions = "Pregnant women should consult a physician.",
                Variants = new() { ("60 Veg Capsules", "60 Caps", 490m, 599m, 50), ("120 Veg Capsules Value", "120 Caps", 890m, 1099m, 30) },
                Reviews = new() { (5, "Remarkable improvement in sleep architecture and morning calm.", 34, 5) }
            },
            new()
            {
                Title = "Ashwagandharishta Classical Fermented Tonic",
                Brand = "AyurVeda Sanjeevani",
                CategoryName = "Stress, Sleep & Brain",
                SubcategoryName = "Ashwagandha",
                Description = "Classical Ayurvedic nerve tonic prepared by natural fermentation. Relieves nervous exhaustion, mental fatigue, and anxiety symptoms.",
                Price = 290m, Mrp = 350m, Stock = 45, Rating = 4.7,
                ImageUrl = "/images/products/triphala.svg", Sku = "AS-STR-002",
                Tags = "ashwagandharishta, nerve tonic, anxiety, vitality",
                TraditionallyUsedFor = "Nervous Exhaustion, Vata Imbalance, General Anxiety",
                Ingredients = "Ashwagandha, Musali, Manjistha, Haritaki, Haridra, Chandan, Jaggery, Dhataki",
                HowToUse = "15-20 ml twice daily with equal quantity of water after meals.",
                Precautions = "Contains naturally occurring alcohol; not suitable during pregnancy."
            },

            // 6. Stress, Sleep & Brain -> Brahmi
            new()
            {
                Title = "Brahmi Vati Medhya Rasayana Tablets",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Stress, Sleep & Brain",
                SubcategoryName = "Brahmi",
                Description = "Classical brain rejuvenator formulation featuring Bacopa monnieri, Shankhpushpi, and Vacha for memory, cognitive acuity, and focus.",
                Price = 320m, Mrp = 399m, Stock = 50, Rating = 4.8,
                ImageUrl = "/images/products/ashwagandha.svg", Sku = "TGA-STR-003",
                Tags = "brahmi, medhya rasayana, memory, concentration, student focus",
                TraditionallyUsedFor = "Memory Retention, Focus, Mental Clarity, Speech Fluency",
                Ingredients = "Bacopa monnieri (Brahmi), Convolvulus pluricaulis (Shankhpushpi), Acorus calamus (Vacha), Swarna Makshika",
                HowToUse = "1 tablet twice daily with honey or warm milk.",
                Precautions = "Do not exceed stated dosage."
            },
            new()
            {
                Title = "Saraswatarishta Memory & Cognitive Elixir",
                Brand = "VaidyaRoots",
                CategoryName = "Stress, Sleep & Brain",
                SubcategoryName = "Brahmi",
                Description = "The classical elixir dedicated to Goddess Saraswati. Formulated with Brahmi, Gold bhasma, and adaptogens for enhanced intellect.",
                Price = 380m, Mrp = 460m, Stock = 35, Rating = 4.8,
                ImageUrl = "/images/products/triphala.svg", Sku = "VR-STR-004",
                Tags = "saraswatarishta, intellect, speech, memory, golden tonic",
                TraditionallyUsedFor = "Cognitive Stamina, Vocal Clarity, Emotional Calm",
                Ingredients = "Brahmi, Shatavari, Vidari, Abhaya, Usheera, Swarna Bhasma, Honey, Jaggery",
                HowToUse = "12-24 ml with equal water twice daily after food.",
                Precautions = "Store bottle tightly closed in a cool place."
            },

            // 6. Stress, Sleep & Brain -> Sleep Support
            new()
            {
                Title = "Nidra-Shanti Deep Sleep Herbal Infusion",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Stress, Sleep & Brain",
                SubcategoryName = "Sleep Support",
                Description = "Botanical bedtime blend of Tagara (Indian Valerian), Jatamansi, Chamomile, and Nutmeg. Promotes deep REM cycles without next-day drowsiness.",
                Price = 340m, Mrp = 425m, Stock = 50, Rating = 4.8,
                ImageUrl = "/images/products/triphala.svg", Sku = "TGA-STR-005",
                Tags = "sleep tea, tagara, jatamansi, insomnia, relaxation",
                TraditionallyUsedFor = "Restful Sleep Induction, Calming Overactive Mind, Vata Pacification",
                Ingredients = "Valeriana wallichii (Tagara), Nardostachys jatamansi, Myristica fragrans (Jaiphal), Matricaria chamomilla",
                HowToUse = "Drink 1 cup 45 minutes before intended sleep time.",
                Precautions = "Do not drive or operate machinery after consumption."
            },
            new()
            {
                Title = "Sarpagandha & Jatamansi Calm Tablets",
                Brand = "Dhanwantari Naturals",
                CategoryName = "Stress, Sleep & Brain",
                SubcategoryName = "Sleep Support",
                Description = "Potent classical tablets formulated with Rauwolfia serpentina and Jatamansi for balancing heightened Vata in the nervous system.",
                Price = 280m, Mrp = 350m, Stock = 0, Rating = 4.7, // Realistic out of stock item
                ImageUrl = "/images/products/ashwagandha.svg", Sku = "DN-STR-006",
                Tags = "sarpagandha, jatamansi, out of stock, blood pressure, calming",
                TraditionallyUsedFor = "Nervous Restlessness, Healthy Blood Pressure, Sleep Ease",
                Ingredients = "Rauwolfia serpentina, Nardostachys jatamansi, Khurasani ajwain",
                HowToUse = "1 tablet under medical supervision at night.",
                Precautions = "Prescription recommended. Avoid in low blood pressure."
            },

            // 7. Diabetes & Metabolism Support -> Herbal Juices
            new()
            {
                Title = "Karela & Jamun Pure Botanical Juice",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Diabetes & Metabolism Support",
                SubcategoryName = "Herbal Juices",
                Description = "Cold-extracted juice from wild bitter gourd (Karela) and organic Java plum (Jamun) seeds. Traditionally used for regulating glucose metabolism.",
                Price = 260m, Mrp = 320m, Stock = 70, Rating = 4.7,
                ImageUrl = "/images/products/triphala.svg", Sku = "TGA-DIA-001",
                Tags = "karela jamun juice, diabetes, blood sugar, metabolism",
                TraditionallyUsedFor = "Blood Glucose Balance, Pancreatic Tone, Metabolic Agni",
                Ingredients = "Momordica charantia (Karela) fruit 50%, Syzygium cumini (Jamun) seeds 50%",
                HowToUse = "30ml mixed with a glass of water first thing in the morning on an empty stomach.",
                Precautions = "Monitor blood sugar regularly if taking alongside conventional medication.",
                Variants = new() { ("1 Litre Bottle", "1L", 260m, 320m, 45), ("2 Litre Twin Pack", "2x1L", 480m, 620m, 25) }
            },
            new()
            {
                Title = "Amla & Aloe Vera Detox Wellness Juice",
                Brand = "VaidyaRoots",
                CategoryName = "Diabetes & Metabolism Support",
                SubcategoryName = "Herbal Juices",
                Description = "Fibrous raw Aloe Vera gel combined with wild mountain Amla. Cleanses metabolic toxins (Ama) and balances liver enzymatic function.",
                Price = 290m, Mrp = 350m, Stock = 45, Rating = 4.8,
                ImageUrl = "/images/products/triphala.svg", Sku = "VR-DIA-002",
                Tags = "aloe amla juice, detox, liver, weight management, digestion",
                TraditionallyUsedFor = "Liver Detoxification, Acid Neutralization, Metabolism Kindle",
                Ingredients = "Organic Aloe barbadensis inner leaf gel 60%, Wild Emblica officinalis juice 40%",
                HowToUse = "25ml diluted with warm water twice daily before meals.",
                Precautions = "Shake well before use; refrigerate after opening."
            },

            // 7. Diabetes & Metabolism Support -> Tablets
            new()
            {
                Title = "Madhumeh-Nashini Herbal Tablets",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Diabetes & Metabolism Support",
                SubcategoryName = "Tablets",
                Description = "Proprietary formulation of Gudmar (sugar destroyer), Vijaysar bark, Jamun, and Methi seeds to support healthy insulin sensitivity.",
                Price = 380m, Mrp = 475m, Stock = 60, Rating = 4.8,
                ImageUrl = "/images/products/ashwagandha.svg", Sku = "TGA-DIA-003",
                Tags = "gudmar, vijaysar, blood sugar, diabetic tablets, metabolism",
                TraditionallyUsedFor = "Sugar Craving Curb, Pancreatic Health, Glucose Absorption",
                Ingredients = "Gymnema sylvestre (Gudmar), Pterocarpus marsupium (Vijaysar), Syzygium cumini, Trigonella foenum-graecum",
                HowToUse = "1-2 tablets twice daily 30 minutes before meals with water.",
                Precautions = "Check fasting and post-meal glucose frequently."
            },
            new()
            {
                Title = "Chandraprabha Vati Urinary & Metabolic Complex",
                Brand = "AyurVeda Sanjeevani",
                CategoryName = "Diabetes & Metabolism Support",
                SubcategoryName = "Tablets",
                Description = "Celebrated classical medicine described in Sharangadhara Samhita. Clears urinary tract, balances glucose loss in urine, and rejuvenates kidney Agni.",
                Price = 250m, Mrp = 310m, Stock = 55, Rating = 4.7,
                ImageUrl = "/images/products/ashwagandha.svg", Sku = "AS-DIA-004",
                Tags = "chandraprabha vati, urinary, kidney, diabetes, shilajit",
                TraditionallyUsedFor = "Urinary Weakness, Glucose Excretion Balance, General Strength",
                Ingredients = "Shuddha Shilajit, Guggulu, Karpoora, Vacha, Musta, Triphala, Trikatu, Chavya, Vidanga",
                HowToUse = "2 tablets twice daily with water or milk.",
                Precautions = "Contains iron bhasma; do not exceed dosage."
            },

            // 7. Diabetes & Metabolism Support -> Teas
            new()
            {
                Title = "Cinnamon & Vijaysar Herbal Glucose Tea",
                Brand = "Himalayan Vana",
                CategoryName = "Diabetes & Metabolism Support",
                SubcategoryName = "Teas",
                Description = "A warming infusion of authentic Ceylon Cinnamon bark, heartwood of Vijaysar, and Jamun seed nibs for balanced glycemic control.",
                Price = 310m, Mrp = 390m, Stock = 40, Rating = 4.7,
                ImageUrl = "/images/products/triphala.svg", Sku = "HV-DIA-005",
                Tags = "cinnamon tea, vijaysar, glucose tea, metabolism",
                TraditionallyUsedFor = "Post-Meal Glucose Moderation, Circulation Support",
                Ingredients = "Cinnamomum verum, Pterocarpus marsupium, Syzygium cumini, Gymnema leaf",
                HowToUse = "Steep 1 tea bag in freshly boiled water for 6 minutes after lunch and dinner.",
                Precautions = "Safe for daily consumption."
            },
            new()
            {
                Title = "Triphala Agni Green Tea",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Diabetes & Metabolism Support",
                SubcategoryName = "Teas",
                Description = "Whole organic green tea leaves blended with Haritaki, Bibhitaki, and Amalaki for antioxidant defense and fat metabolism.",
                Price = 275m, Mrp = 340m, Stock = 65, Rating = 4.6,
                ImageUrl = "/images/products/triphala.svg", Sku = "TGA-DIA-006",
                Tags = "green tea, triphala, weight balance, antioxidants",
                TraditionallyUsedFor = "Lipid Metabolism, Cell Protection, Sluggish Digestion",
                Ingredients = "Camellia sinensis, Terminalia chebula, Terminalia bellirica, Emblica officinalis",
                HowToUse = "Steep in hot (not boiling) water for 3-4 minutes.",
                Precautions = "Contains mild natural caffeine."
            },

            // 8. Women's Wellness -> Cycle Support
            new()
            {
                Title = "Ashokarishta Classical Uterine Tonic",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Women's Wellness",
                SubcategoryName = "Cycle Support",
                Description = "Classical formulation prepared with Saraca asoca bark, Dhataki, and restorative herbs. Regulates monthly cycle flow and relieves cramp discomfort.",
                Price = 275m, Mrp = 330m, Stock = 60, Rating = 4.8,
                ImageUrl = "/images/products/triphala.svg", Sku = "TGA-WOM-001",
                Tags = "ashokarishta, cycle support, cramps, uterine tonic, pms",
                TraditionallyUsedFor = "Menstrual Comfort, Cramp Relief, Uterine Muscular Tone",
                Ingredients = "Ashoka bark, Dhataki flowers, Musta, Sunthi, Daruharidra, Utpala, Triphala, Jaggery",
                HowToUse = "15-20ml with equal water twice daily after food.",
                Precautions = "Do not take during active heavy bleeding days without consulting a doctor."
            },
            new()
            {
                Title = "Pushyanug Churna Classical Women's Compound",
                Brand = "VaidyaRoots",
                CategoryName = "Women's Wellness",
                SubcategoryName = "Cycle Support",
                Description = "Revered formulation described in Charaka Samhita. Combines 26 herbs to manage excess fluid balance, backache, and cycle regularity.",
                Price = 230m, Mrp = 280m, Stock = 40, Rating = 4.7,
                ImageUrl = "/images/products/triphala.svg", Sku = "VR-WOM-002",
                Tags = "pushyanug churna, leucorrhea, cycle balance, backache",
                TraditionallyUsedFor = "Vaginal Health, Excessive Flow, Pelvic Discomfort",
                Ingredients = "Patha, Jambu, Amra, Pashanabheda, Rasanjana, Lodhra, Gairika, Katphala",
                HowToUse = "1 teaspoon with rice washed water (Tandulodaka) or honey twice daily.",
                Precautions = "Store in an airtight container."
            },

            // 8. Women's Wellness -> Pregnancy & Postnatal Care
            new()
            {
                Title = "Jeerakarishta Postnatal Digestive Tonic",
                Brand = "AyurVeda Sanjeevani",
                CategoryName = "Women's Wellness",
                SubcategoryName = "Pregnancy & Postnatal Care",
                Description = "Fermented cumin-based elixir traditionally administered postpartum to rekindle Agni, cleanse the uterus, and support lactation.",
                Price = 295m, Mrp = 360m, Stock = 35, Rating = 4.8,
                ImageUrl = "/images/products/triphala.svg", Sku = "AS-WOM-003",
                Tags = "jeerakarishta, postpartum, lactation, agni, new mother",
                TraditionallyUsedFor = "Postnatal Recovery, Breastmilk Supply, Digestive Rekindle",
                Ingredients = "Jeeraka (Cumin), Dhataki, Sunthi, Jatiphala, Lavanga, Ela, Tejpatra, Jaggery",
                HowToUse = "15ml twice daily after meals with water.",
                Precautions = "Use under the guidance of your Ayurvedic physician postpartum."
            },
            new()
            {
                Title = "Dhanwantharam Thailam Postnatal Massage Oil",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Women's Wellness",
                SubcategoryName = "Pregnancy & Postnatal Care",
                Description = "Classical Kerala formulation with Bala, Dashamula, and cow milk. Deeply strengthens abdominal wall, soothes back muscles, and restores pelvic strength.",
                Price = 420m, Mrp = 510m, Stock = 45, Rating = 4.9,
                ImageUrl = "/images/products/mahanarayan.svg", Sku = "TGA-WOM-004",
                Tags = "dhanwantharam thailam, postnatal massage, bala, pelvic strength",
                TraditionallyUsedFor = "Abdominal Muscle Tone, Pelvic Rejuvenation, Fatigue Relief",
                Ingredients = "Sida cordifolia (Bala), Dashamula, Cow milk, Sesame oil, 40+ rejuvenating herbs",
                HowToUse = "Warm the oil and massage gently over body and abdomen, followed by a warm bath.",
                Precautions = "External application only."
            },

            // 8. Women's Wellness -> Hormonal Balance
            new()
            {
                Title = "Shatavari Pure Root Extract Capsules (500mg)",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Women's Wellness",
                SubcategoryName = "Hormonal Balance",
                Description = "Pure Asparagus racemosus root extract. Celebrated as the ultimate female adaptogen for hormonal harmony, reproductive vitality, and vitality.",
                Price = 390m, Mrp = 480m, Stock = 70, Rating = 4.9,
                ImageUrl = "/images/products/ashwagandha.svg", Sku = "TGA-WOM-005",
                Tags = "shatavari, hormonal balance, pcos, fertility, women vitality",
                TraditionallyUsedFor = "Hormonal Harmony, Cooling Hot Flashes, Natural Fertility Support",
                Ingredients = "Pure Shatavari Root Extract 500mg",
                HowToUse = "1 capsule twice daily with warm milk or water.",
                Precautions = "Safe for long-term daily use.",
                Variants = new() { ("60 Veg Capsules", "60 Caps", 390m, 480m, 45), ("120 Veg Capsules Value", "120 Caps", 720m, 899m, 25) }
            },
            new()
            {
                Title = "Kanchanar Guggulu Lymph & Thyroid Care",
                Brand = "Dhanwantari Naturals",
                CategoryName = "Women's Wellness",
                SubcategoryName = "Hormonal Balance",
                Description = "Classical formulation for clearing Kapha-Medas blockages. Supports healthy thyroid balance, lymphatic drainage, and ovarian health.",
                Price = 290m, Mrp = 360m, Stock = 50, Rating = 4.7,
                ImageUrl = "/images/products/ashwagandha.svg", Sku = "DN-WOM-006",
                Tags = "kanchanar guggulu, thyroid, pcos, lymphatic, kapha",
                TraditionallyUsedFor = "Thyroid Function Support, PCOD/PCOS Management, Cyst Comfort",
                Ingredients = "Kanchanar bark, Triphala, Trikatu, Varuna, Cardamom, Cinnamon, Shuddha Guggulu",
                HowToUse = "2 tablets twice daily with warm water after meals.",
                Precautions = "Not recommended during pregnancy."
            },

            // 9. Herbs & Raw Powders -> Single Herbs
            new()
            {
                Title = "Organic Moringa Oleifera Leaf Powder",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Herbs & Raw Powders",
                SubcategoryName = "Single Herbs",
                Description = "Shade-dried drumstick leaves rich in bioavailable plant iron, calcium, and vitamins. Nature's complete Ayurvedic superfood.",
                Price = 210m, Mrp = 260m, Stock = 65, Rating = 4.8,
                ImageUrl = "/images/products/triphala.svg", Sku = "TGA-HRB-001",
                Tags = "moringa powder, superfood, iron, vitamins, organic",
                TraditionallyUsedFor = "Energy Boost, Natural Iron Supplementation, Lactation Support",
                Ingredients = "100% Pure Organic Moringa oleifera leaf powder",
                HowToUse = "Mix 1 teaspoon into warm water, smoothies, or lentils daily.",
                Precautions = "Natural earthy taste."
            },
            new()
            {
                Title = "Pure Organic Gokshura Powder",
                Brand = "VaidyaRoots",
                CategoryName = "Herbs & Raw Powders",
                SubcategoryName = "Single Herbs",
                Description = "Finely powdered Tribulus terrestris fruit. Nourishes kidney-bladder channels and supports physical stamina and reproductive health.",
                Price = 230m, Mrp = 290m, Stock = 45, Rating = 4.7,
                ImageUrl = "/images/products/triphala.svg", Sku = "VR-HRB-002",
                Tags = "gokshura, tribulus, kidney, stamina, urinary",
                TraditionallyUsedFor = "Kidney Flush, Physical Endurance, Male & Female Reproductive Tone",
                Ingredients = "100% Pure Tribulus terrestris fruit powder",
                HowToUse = "1/2 to 1 teaspoon twice daily with warm milk or honey.",
                Precautions = "Store in a moisture-free container."
            },

            // 9. Herbs & Raw Powders -> Powders
            new()
            {
                Title = "Pure Organic Amalaki (Amla) Powder",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Herbs & Raw Powders",
                SubcategoryName = "Powders",
                Description = "Raw whole wild Amla fruit powder. Packed with natural heat-stable Vitamin C and potent polyphenols for Pitta pacification.",
                Price = 180m, Mrp = 225m, Stock = 90, Rating = 4.9,
                ImageUrl = "/images/products/triphala.svg", Sku = "TGA-HRB-003",
                Tags = "amla powder, vitamin c, immunity, hair, pitta",
                TraditionallyUsedFor = "Antioxidant Defense, Eye Health, Scalp Nourishment",
                Ingredients = "100% Raw Wild Emblica officinalis powder",
                HowToUse = "1 teaspoon in morning with lukewarm water or raw honey.",
                Precautions = "Naturally tart and astringent."
            },
            new()
            {
                Title = "Shatavari Root Fine Churna",
                Brand = "Himalayan Vana",
                CategoryName = "Herbs & Raw Powders",
                SubcategoryName = "Powders",
                Description = "Pure mountain Shatavari root ground to 100-mesh fineness. Rich in natural cooling mucilage for Pitta soothing.",
                Price = 240m, Mrp = 300m, Stock = 50, Rating = 4.8,
                ImageUrl = "/images/products/triphala.svg", Sku = "HV-HRB-004",
                Tags = "shatavari powder, cooling, pitta soothing, gut lining",
                TraditionallyUsedFor = "Gastric Mucosa Protection, Natural Hydration, Female Vitality",
                Ingredients = "100% Organic Asparagus racemosus root powder",
                HowToUse = "1 teaspoon boiled in a cup of milk with cardamom.",
                Precautions = "Store away from humidity."
            },

            // 9. Herbs & Raw Powders -> Seeds & Spices
            new()
            {
                Title = "Wild Forest Black Jeera (Kala Jeera)",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Herbs & Raw Powders",
                SubcategoryName = "Seeds & Spices",
                Description = "Rare wild Bunium persicum seeds harvested from Himalayan slopes. Superior aroma and profound Agni-stimulating properties.",
                Price = 320m, Mrp = 400m, Stock = 35, Rating = 4.9,
                ImageUrl = "/images/products/triphala.svg", Sku = "TGA-HRB-005",
                Tags = "kala jeera, black cumin, spices, agni, wild spices",
                TraditionallyUsedFor = "Digestive Rekindle, Respiratory Ease, Culinary Medicine",
                Ingredients = "Wild harvested Bunium persicum seeds",
                HowToUse = "Lightly roast and add to warm stews, or chew 1/2 tsp after meals.",
                Precautions = "A little goes a long way due to concentrated oils."
            },
            new()
            {
                Title = "Salem First-Grade Turmeric Finger (Haldi)",
                Brand = "Dhanwantari Naturals",
                CategoryName = "Herbs & Raw Powders",
                SubcategoryName = "Seeds & Spices",
                Description = "Whole sun-dried Salem turmeric fingers with naturally high Curcumin content. Free from polishing agents.",
                Price = 190m, Mrp = 240m, Stock = 80, Rating = 4.8,
                ImageUrl = "/images/products/triphala.svg", Sku = "DN-HRB-006",
                Tags = "salem haldi, whole turmeric, curcumin, raw spice",
                TraditionallyUsedFor = "Immunity Cooking, Anti-Inflammatory Golden Milk, Traditional Remedies",
                Ingredients = "100% Unpolished Curcuma longa rhizomes",
                HowToUse = "Grate fresh into milk or grind for authentic Ayurvedic cooking.",
                Precautions = "Natural yellow dye."
            },

            // 10. Panchakarma & Therapy Kits -> Oils
            new()
            {
                Title = "Anu Thailam Classical Nasya Oil",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Panchakarma & Therapy Kits",
                SubcategoryName = "Oils",
                Description = "Classical Nasya formulation described in Charaka Samhita. 28 herbs processed 10 times in sesame oil to clear sinus passages and enhance mental clarity.",
                Price = 195m, Mrp = 250m, Stock = 75, Rating = 4.9,
                ImageUrl = "/images/products/hair-oil.svg", Sku = "TGA-PAN-001",
                Tags = "anu thailam, nasya, sinus, migraine, prana, panchakarma",
                TraditionallyUsedFor = "Sinus Congestion Relief, Allergy Defense, Neck & Shoulder Ease",
                Ingredients = "Jivanti, Jala, Devadaru, Jalada, Tvak, Sevya, Gopi, Hima, Sesame oil, Goat milk",
                HowToUse = "Instill 2 drops into each nostril in the morning after gentle facial fomentation.",
                Precautions = "Do not administer immediately after meals, bathing, or during acute fever.",
                Reviews = new() { (5, "My chronic morning sinus headaches stopped after 4 days of Nasya. Indispensable.", 26, 11) }
            },
            new()
            {
                Title = "Karpurasava & Murivenna First-Aid Oil",
                Brand = "VaidyaRoots",
                CategoryName = "Panchakarma & Therapy Kits",
                SubcategoryName = "Oils",
                Description = "The revered Kerala herbal oil for sprains, muscle injuries, minor cuts, and bruises. Prepared with Betel leaf, Moringa, and Aloe.",
                Price = 280m, Mrp = 340m, Stock = 50, Rating = 4.8,
                ImageUrl = "/images/products/mahanarayan.svg", Sku = "VR-PAN-002",
                Tags = "murivenna, kerala oil, sprains, bruises, wound healing",
                TraditionallyUsedFor = "Rapid Soft Tissue Recovery, Sprain Relief, Minor Abrasions",
                Ingredients = "Tambula (Betel leaf), Shigru, Kumari, Paribhadra, Coconut oil base",
                HowToUse = "Apply gently over sprained joints or minor cuts.",
                Precautions = "For external application only."
            },

            // 10. Panchakarma & Therapy Kits -> Basti/Nasya/Abhyanga items
            new()
            {
                Title = "Copper Neti Pot for Jala Neti Cleansing",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Panchakarma & Therapy Kits",
                SubcategoryName = "Basti/Nasya/Abhyanga items",
                Description = "Handcrafted pure copper Neti pot with ergonomic spout for traditional nasal irrigation (Jala Neti). Naturally antimicrobial.",
                Price = 580m, Mrp = 750m, Stock = 40, Rating = 4.9,
                ImageUrl = "/images/products/chyawanprash.svg", Sku = "TGA-PAN-003",
                Tags = "copper neti pot, jala neti, sinus wash, panchakarma accessory",
                TraditionallyUsedFor = "Nasal Passage Irrigation, Upper Respiratory Cleansing, Prana Clarity",
                Ingredients = "100% Pure Hand-Spun Copper",
                HowToUse = "Fill with lukewarm sterile saline water. Lean over sink, tilt head, and pour gently through one nostril.",
                Precautions = "Always use distilled or boiled sterile water; never tap water."
            },
            new()
            {
                Title = "Authentic Kansa Vataki Foot Massage Bowl",
                Brand = "AyurVeda Sanjeevani",
                CategoryName = "Panchakarma & Therapy Kits",
                SubcategoryName = "Basti/Nasya/Abhyanga items",
                Description = "Sacred 3-metal bronze alloy (Kansa) bowl. Massaging soles with ghee draws out excess body heat and pacifies fiery Pitta dosha.",
                Price = 720m, Mrp = 950m, Stock = 30, Rating = 4.9,
                ImageUrl = "/images/products/chyawanprash.svg", Sku = "AS-PAN-004",
                Tags = "kansa bowl, foot massage, pada abhyanga, pitta detox",
                TraditionallyUsedFor = "Excess Pitta Detox, Deep Sleep Induction, Foot Fatigue Relief",
                Ingredients = "Vedic Kansa Alloy (80% Copper, 20% Tin)",
                HowToUse = "Apply ghee or castor oil to soles of feet. Rub the bronze bowl in circular motions for 10-15 minutes.",
                Precautions = "Skin may darken temporarily during massage as toxins are drawn out; wipe with a warm cloth."
            },

            // 10. Panchakarma & Therapy Kits -> Accessories
            new()
            {
                Title = "Pure Copper Tongue Cleaner (Scraper)",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Panchakarma & Therapy Kits",
                SubcategoryName = "Accessories",
                Description = "Wide-contoured, thick-gauge 100% copper scraper for morning Dinacharya. Removes overnight toxic coating (Ama) from the tongue.",
                Price = 120m, Mrp = 150m, Stock = 150, Rating = 4.8,
                ImageUrl = "/images/products/triphala.svg", Sku = "TGA-PAN-005",
                Tags = "copper tongue cleaner, dinacharya, ama detox, oral hygiene",
                TraditionallyUsedFor = "Morning Ama Removal, Fresh Breath, Digestive Fire Awakening",
                Ingredients = "Pure Solid Copper",
                HowToUse = "Hold both ends, place on back of tongue, and gently scrape forward 4-5 times.",
                Precautions = "Rinse with warm water after use; clean with lemon and salt if tarnished."
            },
            new()
            {
                Title = "Neem Wood Wide-Tooth De-Tangling Comb",
                Brand = "Himalayan Vana",
                CategoryName = "Panchakarma & Therapy Kits",
                SubcategoryName = "Accessories",
                Description = "Hand-carved from seasoned medicinal Indian Neem wood. Naturally antistatic, stimulates scalp acupressure points, and inhibits dandruff.",
                Price = 160m, Mrp = 200m, Stock = 90, Rating = 4.7,
                ImageUrl = "/images/products/triphala.svg", Sku = "HV-PAN-006",
                Tags = "neem comb, wooden comb, scalp massage, antistatic",
                TraditionallyUsedFor = "Scalp Blood Circulation, Dandruff Prevention, Zero Static",
                Ingredients = "100% Seasoned Azadirachta indica (Neem) wood",
                HowToUse = "Gently comb hair from scalp to ends twice daily.",
                Precautions = "Do not soak in water; wipe clean with a dry cloth."
            },

            // 11. Yoga & Wellness -> Mats & Props
            new()
            {
                Title = "Organic Hand-Woven Cotton Yoga Mat with Anti-Slip Base",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Yoga & Wellness",
                SubcategoryName = "Mats & Props",
                Description = "Loomed by traditional artisans from pure organic cotton, dyed with Ayurvedic herbs (Turmeric & Neem), backed with natural tree rubber.",
                Price = 1850m, Mrp = 2400m, Stock = 25, Rating = 4.9,
                ImageUrl = "/images/yoga/surya-namaskar.svg", Sku = "TGA-YOG-001",
                Tags = "cotton yoga mat, organic mat, natural rubber, herbal dyed",
                TraditionallyUsedFor = "Grounded Asana Practice, Zero Plastic Living, Sweat Absorption",
                Ingredients = "Organic Indian Cotton, Herbal dyes, Natural Tree Rubber bottom",
                HowToUse = "Unroll on flat surface for yoga, pranayama, and meditation practice.",
                Precautions = "Spot clean with gentle wet sponge; dry in shade.",
                Variants = new() { ("Forest Green 4mm", "Green 4mm", 1850m, 2400m, 15), ("Earthy Turmeric 4mm", "Gold 4mm", 1850m, 2400m, 10) }
            },
            new()
            {
                Title = "Buckwheat Hull Organic Meditation Cushion (Zafu)",
                Brand = "Himalayan Vana",
                CategoryName = "Yoga & Wellness",
                SubcategoryName = "Mats & Props",
                Description = "Ergonomic meditation cushion filled with organic buckwheat hulls. Supports proper pelvic tilt and aligned spine during meditation.",
                Price = 1290m, Mrp = 1650m, Stock = 20, Rating = 4.8,
                ImageUrl = "/images/yoga/vrikshasana.svg", Sku = "HV-YOG-002",
                Tags = "zafu, meditation cushion, buckwheat, yoga prop",
                TraditionallyUsedFor = "Spinal Alignment, Hip Ease, Prolonged Dhyana Practice",
                Ingredients = "Heavy cotton canvas outer cover, Organic Buckwheat hull filling",
                HowToUse = "Sit cross-legged with pelvis raised on cushion.",
                Precautions = "Cover is machine washable; do not wash buckwheat filling."
            },

            // 11. Yoga & Wellness -> Books
            new()
            {
                Title = "The Living Ayurveda Handbook: Daily Dinacharya & Food Wisdom",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Yoga & Wellness",
                SubcategoryName = "Books",
                Description = "A masterclass hardcover guide translating classical Sanskrit texts into practical daily routines, dosha balancing meals, and home herbal remedies.",
                Price = 499m, Mrp = 699m, Stock = 45, Rating = 4.9,
                ImageUrl = "/images/yoga/surya-namaskar.svg", Sku = "TGA-YOG-003",
                Tags = "ayurveda book, dinacharya, recipes, lifestyle guide",
                TraditionallyUsedFor = "Self-Healing Knowledge, Dosha Identification, Dietary Wisdom",
                Ingredients = "Sustainable Recycled Paper, Hardcover Binding, 280 Full-Color Pages",
                HowToUse = "Read sequentially or reference seasonal lifestyle chapters.",
                Precautions = "Comprehensive reference for the modern home."
            },
            new()
            {
                Title = "Ashtanga Hridaya: Classical Pocket Guide for Modern Life",
                Brand = "AyurVeda Sanjeevani",
                CategoryName = "Yoga & Wellness",
                SubcategoryName = "Books",
                Description = "Sanskrit slokas with English commentary detailing Vagbhata's eternal core principles of health, digestion, and longevity.",
                Price = 350m, Mrp = 450m, Stock = 30, Rating = 4.7,
                ImageUrl = "/images/yoga/vrikshasana.svg", Sku = "AS-YOG-004",
                Tags = "ashtanga hridaya, vagbhata, classical text, philosophy",
                TraditionallyUsedFor = "Classical Study, Vaidya Fundamentals, Holistic Philosophy",
                Ingredients = "Compact paperback edition, 210 pages",
                HowToUse = "Ideal study companion for wellness enthusiasts.",
                Precautions = "Educational reference."
            },

            // 11. Yoga & Wellness -> Aromatherapy
            new()
            {
                Title = "Pure Vetiver (Khus) Essential Oil",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Yoga & Wellness",
                SubcategoryName = "Aromatherapy",
                Description = "Hydro-distilled roots of Chrysopogon zizanioides. The quintessential 'Oil of Tranquility' that pacifies agitated Pitta and grounds restless Vata.",
                Price = 640m, Mrp = 800m, Stock = 35, Rating = 4.8,
                ImageUrl = "/images/products/hair-oil.svg", Sku = "TGA-YOG-005",
                Tags = "khus oil, vetiver, essential oil, aromatherapy, pitta cooling",
                TraditionallyUsedFor = "Deep Grounding, Calming Anger, Night Diffuser Ritual",
                Ingredients = "100% Pure Steam Distilled Vetiveria zizanioides root oil",
                HowToUse = "Add 3-4 drops to an ultrasonic diffuser or mix 2 drops with jojoba oil for pulse points.",
                Precautions = "Never apply undiluted directly to bare skin."
            },
            new()
            {
                Title = "Temple Camphor & Loban Botanical Incense Cones",
                Brand = "Dhanwantari Naturals",
                CategoryName = "Yoga & Wellness",
                SubcategoryName = "Aromatherapy",
                Description = "Charcoal-free dhoop cones crafted with temple flower offerings, pure Bhimseni camphor, and natural Frankincense (Loban) resin.",
                Price = 180m, Mrp = 230m, Stock = 80, Rating = 4.7,
                ImageUrl = "/images/yoga/surya-namaskar.svg", Sku = "DN-YOG-006",
                Tags = "loban, dhoop cones, incense, charcoal free, purification",
                TraditionallyUsedFor = "Space Cleansing, Meditation Atmosphere, Mosquito Deterrent",
                Ingredients = "Boswellia serrata resin, Pure Camphor, Sacred wood powders, Essential oils",
                HowToUse = "Light the tip of the cone, gently blow out flame, and place on heatproof ceramic dish.",
                Precautions = "Keep burning cones away from flammable curtains."
            },

            // 12. Honey, Ghee & Natural Foods -> Honey
            new()
            {
                Title = "Wild Forest Multi-Flora Raw Honey",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Honey, Ghee & Natural Foods",
                SubcategoryName = "Honey",
                Description = "Ethically collected raw honey from untouched Himalayan forests. Unheated, unpasteurized, and rich in natural pollen, enzymes, and Yogavahi power.",
                Price = 380m, Mrp = 460m, Stock = 65, Rating = 4.9,
                ImageUrl = "/images/products/chyawanprash.svg", Sku = "TGA-FOD-001",
                Tags = "raw honey, forest honey, wild honey, yogavahi, unheated",
                TraditionallyUsedFor = "Anupana (Herbal Vehicle), Throat Comfort, Kapha Scraping",
                Ingredients = "100% Raw Multi-Flora Wild Forest Honey",
                HowToUse = "1 teaspoon daily with room temperature water or herbal churnas.",
                Precautions = "Never cook or add honey to boiling hot liquids above 40°C as per Ayurvedic Charaka injunctions.",
                Variants = new() { ("500g Glass Jar", "500g", 380m, 460m, 40), ("1kg Glass Jar", "1kg", 690m, 850m, 25) },
                Reviews = new() { (5, "You can smell the wild flora. True raw honey that crystallizes naturally.", 21, 6) }
            },
            new()
            {
                Title = "Kashmir Acacia Mono-Floral Raw Honey",
                Brand = "Himalayan Vana",
                CategoryName = "Honey, Ghee & Natural Foods",
                SubcategoryName = "Honey",
                Description = "Water-clear, light floral raw honey collected exclusively during spring Robinia pseudoacacia blooms in Kashmir valleys.",
                Price = 520m, Mrp = 650m, Stock = 30, Rating = 4.8,
                ImageUrl = "/images/products/chyawanprash.svg", Sku = "HV-FOD-002",
                Tags = "acacia honey, mono floral, kashmir honey, delicate",
                TraditionallyUsedFor = "Gentle Sweetener, Delicate Digestion, Children's Tonic",
                Ingredients = "100% Pure Acacia raw honey",
                HowToUse = "Add to lukewarm tisanes or drizzle over fruit.",
                Precautions = "Do not give honey to infants under 12 months."
            },

            // 12. Honey, Ghee & Natural Foods -> Cow Ghee
            new()
            {
                Title = "Vedic A2 Gir Cow Bilona Ghee",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Honey, Ghee & Natural Foods",
                SubcategoryName = "Cow Ghee",
                Description = "Prepared strictly according to the traditional Vedic 5-step Bilona method: Grass-fed Gir cow milk cultured into whole curd, then hand-churned in wooden pots and slow-simmered.",
                Price = 1150m, Mrp = 1450m, Stock = 45, Rating = 5.0,
                ImageUrl = "/images/products/chyawanprash.svg", Sku = "TGA-FOD-003",
                Tags = "a2 ghee, gir cow, bilona ghee, ojas, grass fed",
                TraditionallyUsedFor = "Ojas Vitality, Agni Kindle, Deep Brain Tissue Nourishment",
                Ingredients = "100% Pure Clarified Butter from Vedic Gir Cow Cultured Curd",
                HowToUse = "Add 1-2 teaspoons over warm rotis, steamed rice, or consume 1 tsp with warm water upon waking.",
                Precautions = "Gold standard Rasayana; store at room temperature.",
                Variants = new() { ("500ml Glass Jar", "500ml", 1150m, 1450m, 30), ("1 Litre Glass Jar", "1L", 2150m, 2750m, 15) },
                Reviews = new() { (5, "Grainy golden texture with irresistible nutty aroma. The best ghee in India.", 42, 2) },
                Faqs = new() { ("Is this made from cultured curd or cream?", "Strictly made from whole cultured A2 curd, never directly from cream.", "Ananya M.") }
            },
            new()
            {
                Title = "Brahmi-Infused Medhya Ghritha",
                Brand = "VaidyaRoots",
                CategoryName = "Honey, Ghee & Natural Foods",
                SubcategoryName = "Cow Ghee",
                Description = "Pure A2 cow ghee slow-cooked with fresh Brahmi juice and Shankhpushpi decoction. Crosses the blood-brain barrier to nourish mental faculties.",
                Price = 780m, Mrp = 950m, Stock = 25, Rating = 4.9,
                ImageUrl = "/images/products/chyawanprash.svg", Sku = "VR-FOD-004",
                Tags = "brahmi ghee, medhya ghritha, cognitive food, kids memory",
                TraditionallyUsedFor = "Mental Stamina, Memory Retention, Calming Vata Agitation",
                Ingredients = "A2 Cow Ghee, Bacopa monnieri fresh herb, Convolvulus pluricaulis, Licorice",
                HowToUse = "1 teaspoon melted into warm milk on empty stomach in morning.",
                Precautions = "Suitable for students and working professionals."
            },

            // 12. Honey, Ghee & Natural Foods -> Herbal Sweeteners
            new()
            {
                Title = "Organic Kolhapur Block Jaggery (Gur)",
                Brand = "Great Ayurveda Labs",
                CategoryName = "Honey, Ghee & Natural Foods",
                SubcategoryName = "Herbal Sweeteners",
                Description = "Traditional unrefined sugarcane jaggery prepared without chemical bleaches or synthetic sulfur. Rich in natural bioavailable minerals and plant iron.",
                Price = 140m, Mrp = 175m, Stock = 80, Rating = 4.8,
                ImageUrl = "/images/products/chyawanprash.svg", Sku = "TGA-FOD-005",
                Tags = "jaggery, organic gur, iron rich, unrefined sugar, mineral rich",
                TraditionallyUsedFor = "Digestive Tonic After Meals, Natural Iron, Healthy Sweetener",
                Ingredients = "100% Pure Organic Sugarcane Juice",
                HowToUse = "Consume a small piece after meals to aid digestion, or use in traditional herbal preparations.",
                Precautions = "Keep in an airtight jar to prevent moisture absorption."
            },
            new()
            {
                Title = "Mishri (Unrefined Rock Sugar Crystals)",
                Brand = "Dhanwantari Naturals",
                CategoryName = "Honey, Ghee & Natural Foods",
                SubcategoryName = "Herbal Sweeteners",
                Description = "Authentic Dhaga Mishri (threaded rock sugar) formed by natural slow evaporation. Considered inherently cooling and tridoshic in classical pharmacopoeia.",
                Price = 160m, Mrp = 200m, Stock = 60, Rating = 4.7,
                ImageUrl = "/images/products/triphala.svg", Sku = "DN-FOD-006",
                Tags = "mishri, dhaga mishri, rock sugar, cooling, throat soothe",
                TraditionallyUsedFor = "Throat Comfort with Saunf, Pitta Soothing, Energy Drink",
                Ingredients = "100% Natural Crystallized Cane Sugar (Threaded)",
                HowToUse = "Powder and combine with fennel seeds (Saunf) as a post-meal palate cleanser.",
                Precautions = "Pure crystallized sugar; consume in moderation."
            }
        };
    }

    public static async Task AlignDatabaseSchemaAndFixDataAsync(
        AyurvedaDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<int>> roleManager,
        ILogger logger,
        bool isDevelopment = true)
    {
        try
        {
            // 1. Schema repairs & constraint cleanup
            var sqlSchemaRepair = @"
-- Check and add/fix columns in Carts
IF OBJECT_ID('Carts', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('Carts', 'UpdatedAt') IS NULL
    BEGIN
        ALTER TABLE [Carts] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE();
    END
    ELSE
    BEGIN
        IF NOT EXISTS (
            SELECT 1 FROM sys.default_constraints dc
            JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
            WHERE dc.parent_object_id = OBJECT_ID('Carts') AND c.name = 'UpdatedAt'
        )
        BEGIN
            ALTER TABLE [Carts] ADD CONSTRAINT [DF_Carts_UpdatedAt] DEFAULT GETUTCDATE() FOR [UpdatedAt];
        END
        UPDATE [Carts] SET [UpdatedAt] = ISNULL([CreatedAt], GETUTCDATE()) WHERE [UpdatedAt] IS NULL;
    END
END

-- Drop legacy foreign keys pointing to Users table
IF OBJECT_ID('FK_Appointments_Users_UserId', 'F') IS NOT NULL
    ALTER TABLE [Appointments] DROP CONSTRAINT [FK_Appointments_Users_UserId];
IF OBJECT_ID('FK_Appointments_Users_UserId1', 'F') IS NOT NULL
    ALTER TABLE [Appointments] DROP CONSTRAINT [FK_Appointments_Users_UserId1];
IF OBJECT_ID('FK_Carts_Users_UserId', 'F') IS NOT NULL
    ALTER TABLE [Carts] DROP CONSTRAINT [FK_Carts_Users_UserId];
IF OBJECT_ID('FK_Orders_Users_UserId', 'F') IS NOT NULL
    ALTER TABLE [Orders] DROP CONSTRAINT [FK_Orders_Users_UserId];
IF OBJECT_ID('FK_Orders_Users_UserId1', 'F') IS NOT NULL
    ALTER TABLE [Orders] DROP CONSTRAINT [FK_Orders_Users_UserId1];
IF OBJECT_ID('FK_ProductReviews_Users_UserId', 'F') IS NOT NULL
    ALTER TABLE [ProductReviews] DROP CONSTRAINT [FK_ProductReviews_Users_UserId];
IF OBJECT_ID('FK_YogaReviews_Users_UserId', 'F') IS NOT NULL
    ALTER TABLE [YogaReviews] DROP CONSTRAINT [FK_YogaReviews_Users_UserId];

-- Reconcile any orphan UserIds before adding foreign keys
DECLARE @fallbackUserId int = (SELECT TOP 1 Id FROM AspNetUsers WHERE Email = 'customer@ayurveda.com');
IF @fallbackUserId IS NULL SET @fallbackUserId = (SELECT TOP 1 Id FROM AspNetUsers ORDER BY Id);
IF @fallbackUserId IS NOT NULL
BEGIN
    UPDATE Appointments SET UserId = @fallbackUserId WHERE UserId NOT IN (SELECT Id FROM AspNetUsers);
    UPDATE Carts SET UserId = @fallbackUserId WHERE UserId NOT IN (SELECT Id FROM AspNetUsers);
    UPDATE Orders SET UserId = @fallbackUserId WHERE UserId NOT IN (SELECT Id FROM AspNetUsers);
    UPDATE ProductReviews SET UserId = @fallbackUserId WHERE UserId NOT IN (SELECT Id FROM AspNetUsers);
    UPDATE YogaReviews SET UserId = @fallbackUserId WHERE UserId NOT IN (SELECT Id FROM AspNetUsers);
END

-- Re-point FK constraints to AspNetUsers if missing
IF OBJECT_ID('FK_Appointments_AspNetUsers_UserId', 'F') IS NULL
    ALTER TABLE [Appointments] ADD CONSTRAINT [FK_Appointments_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;
IF OBJECT_ID('FK_Carts_AspNetUsers_UserId', 'F') IS NULL
    ALTER TABLE [Carts] ADD CONSTRAINT [FK_Carts_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;
IF OBJECT_ID('FK_Orders_AspNetUsers_UserId', 'F') IS NULL
    ALTER TABLE [Orders] ADD CONSTRAINT [FK_Orders_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;
IF OBJECT_ID('FK_ProductReviews_AspNetUsers_UserId', 'F') IS NULL
    ALTER TABLE [ProductReviews] ADD CONSTRAINT [FK_ProductReviews_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;
IF OBJECT_ID('FK_YogaReviews_AspNetUsers_UserId', 'F') IS NULL
    ALTER TABLE [YogaReviews] ADD CONSTRAINT [FK_YogaReviews_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;

-- Ensure Banner columns exist
IF OBJECT_ID('Banners', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('Banners', 'Placement') IS NULL
        ALTER TABLE [Banners] ADD [Placement] nvarchar(50) NOT NULL DEFAULT 'home_hero';
    IF COL_LENGTH('Banners', 'StartDate') IS NULL
        ALTER TABLE [Banners] ADD [StartDate] datetime2 NULL;
    IF COL_LENGTH('Banners', 'EndDate') IS NULL
        ALTER TABLE [Banners] ADD [EndDate] datetime2 NULL;
    IF COL_LENGTH('Banners', 'UpdatedAt') IS NULL
        ALTER TABLE [Banners] ADD [UpdatedAt] datetime2 NULL DEFAULT GETUTCDATE();
END

-- Ensure Coupon columns exist
IF OBJECT_ID('Coupons', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('Coupons', 'StartDate') IS NULL
        ALTER TABLE [Coupons] ADD [StartDate] datetime2 NULL;
    IF COL_LENGTH('Coupons', 'ApplicableCategory') IS NULL
        ALTER TABLE [Coupons] ADD [ApplicableCategory] nvarchar(100) NULL;
    IF COL_LENGTH('Coupons', 'UpdatedAt') IS NULL
        ALTER TABLE [Coupons] ADD [UpdatedAt] datetime2 NULL DEFAULT GETUTCDATE();
END

IF COL_LENGTH('Banners', 'UpdatedAt') IS NOT NULL AND COL_LENGTH('Banners', 'CreatedAt') IS NOT NULL
    EXEC('UPDATE [Banners] SET [UpdatedAt] = ISNULL([CreatedAt], GETUTCDATE()) WHERE [UpdatedAt] IS NULL;');
IF COL_LENGTH('Coupons', 'UpdatedAt') IS NOT NULL AND COL_LENGTH('Coupons', 'CreatedAt') IS NOT NULL
    EXEC('UPDATE [Coupons] SET [UpdatedAt] = ISNULL([CreatedAt], GETUTCDATE()) WHERE [UpdatedAt] IS NULL;');
IF COL_LENGTH('Carts', 'UpdatedAt') IS NOT NULL AND COL_LENGTH('Carts', 'CreatedAt') IS NOT NULL
    EXEC('UPDATE [Carts] SET [UpdatedAt] = ISNULL([CreatedAt], GETUTCDATE()) WHERE [UpdatedAt] IS NULL;');
";
            await context.Database.ExecuteSqlRawAsync(sqlSchemaRepair);

            // 2. Sync AspNetUsers and legacy Users table bidirectionally
            var sqlSyncUsers = @"
IF OBJECT_ID('Users', 'U') IS NOT NULL AND OBJECT_ID('AspNetUsers', 'U') IS NOT NULL
BEGIN
    SET IDENTITY_INSERT Users ON;
    INSERT INTO Users (Id, Username, Email, PasswordHash, Contact, Role, AccessFailedCount, CreatedAt)
    SELECT u.Id, 
           ISNULL(u.UserName, u.Email), 
           ISNULL(u.Email, 'user' + CAST(u.Id AS nvarchar(20)) + '@thegreatayurveda.com'), 
           ISNULL(u.PasswordHash, ''), 
           ISNULL(u.ContactNumber, ''), 
           'Customer',
           0,
           ISNULL(u.CreatedAt, GETUTCDATE())
    FROM AspNetUsers u
    WHERE NOT EXISTS (SELECT 1 FROM Users lu WHERE lu.Id = u.Id);
    SET IDENTITY_INSERT Users OFF;

    SET IDENTITY_INSERT AspNetUsers ON;
    INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, FullName, CreatedAt, IsActive)
    SELECT lu.Id, 
           lu.Username, 
           UPPER(lu.Username), 
           lu.Email, 
           UPPER(lu.Email), 
           1, 
           lu.PasswordHash, 
           NEWID(), 
           NEWID(), 
           lu.Contact, 
           0, 0, 0, 0, 
           lu.Username, 
           ISNULL(lu.CreatedAt, GETUTCDATE()), 
           1
    FROM Users lu
    WHERE NOT EXISTS (SELECT 1 FROM AspNetUsers au WHERE au.Id = lu.Id);
    SET IDENTITY_INSERT AspNetUsers OFF;
END
";
            await context.Database.ExecuteSqlRawAsync(sqlSyncUsers);

            if (isDevelopment)
            {
                // 3. Seed demo customer user in Development only
                var customerEmail = "customer@ayurveda.com";
                var customerUser = await userManager.FindByEmailAsync(customerEmail);
                if (customerUser == null)
                {
                    customerUser = new ApplicationUser
                    {
                        UserName = customerEmail,
                        Email = customerEmail,
                        FullName = "Aarav Patel",
                        ContactNumber = "+91 98765 12345",
                        EmailConfirmed = true,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    var custRes = await userManager.CreateAsync(customerUser, "DevCustomer@2026");
                    if (custRes.Succeeded)
                    {
                        await userManager.AddToRoleAsync(customerUser, UserRole.Customer.ToString());
                        logger.LogInformation("Seeded standard demo customer {Email}", customerEmail);
                    }
                }
            }

            // 4. Image Repair Step
            await RepairBrokenImagesAsync(context, logger);

            // 5. Seed 8+ Banners
            await SeedOrUpdateBannersAsync(context, logger);

            // 6. Seed Festival Offers
            await SeedFestivalOffersAsync(context, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing AlignDatabaseSchemaAndFixDataAsync");
        }
    }

    private static bool ImageFileExists(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl)) return false;
        var trimmed = relativeUrl.TrimStart('~').TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var cur = Directory.GetCurrentDirectory();
        
        var paths = new[]
        {
            Path.Combine(cur, "wwwroot", trimmed),
            Path.Combine(cur, "..", "Ayurveda.Web", "wwwroot", trimmed),
            Path.Combine(cur, "..", "Ayurveda.Admin", "wwwroot", trimmed),
            Path.Combine(cur, "src", "Ayurveda.Web", "wwwroot", trimmed),
            Path.Combine(cur, "src", "Ayurveda.Admin", "wwwroot", trimmed)
        };

        foreach (var p in paths)
        {
            try
            {
                if (File.Exists(p)) return true;
            }
            catch { }
        }
        return false;
    }

    private static async Task RepairBrokenImagesAsync(AyurvedaDbContext context, ILogger logger)
    {
        // 1. Categories
        var categories = await context.Categories.ToListAsync();
        var catSvgMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Digestive Care", "/images/categories/digestion.svg" },
            { "Digestive Health", "/images/categories/digestion.svg" },
            { "Skin Care", "/images/categories/skin.svg" },
            { "Hair & Skin Care", "/images/categories/skin.svg" },
            { "Immunity & Rasayana", "/images/categories/immunity.svg" },
            { "Immunity & Rejuvenation", "/images/categories/immunity.svg" },
            { "Joint & Muscle Care", "/images/categories/joints.svg" },
            { "Joint & Bone Care", "/images/categories/joints.svg" },
            { "Stress, Sleep & Brain", "/images/categories/sleep.svg" },
            { "Mental Wellness", "/images/categories/sleep.svg" },
            { "Hair Care", "/images/categories/hair.svg" },
            { "Diabetes & Metabolism Support", "/images/categories/metabolism.svg" },
            { "Women's Wellness", "/images/categories/womens.svg" },
            { "Herbs & Raw Powders", "/images/categories/herbs.svg" },
            { "Panchakarma & Therapy Kits", "/images/categories/panchakarma.svg" },
            { "Yoga & Wellness", "/images/categories/yoga.svg" },
            { "Honey, Ghee & Natural Foods", "/images/categories/foods.svg" }
        };

        foreach (var c in categories)
        {
            if (string.IsNullOrWhiteSpace(c.ImageUrl) || !ImageFileExists(c.ImageUrl) || c.ImageUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                if (catSvgMap.TryGetValue(c.Name, out var targetSvg))
                {
                    c.ImageUrl = targetSvg;
                }
                else
                {
                    c.ImageUrl = "/images/categories/digestion.svg";
                }
            }
        }

        // 2. Doctors
        var doctors = await context.Doctors.ToListAsync();
        foreach (var d in doctors)
        {
            if (string.IsNullOrWhiteSpace(d.ProfileImage) || !ImageFileExists(d.ProfileImage))
            {
                int idx = ((d.Id - 1) % 24) + 1;
                d.ProfileImage = $"/images/doctors/doctor-{idx}.svg";
            }
        }

        // 3. Products
        var products = await context.Products.Include(p => p.Category).Include(p => p.Images).ToListAsync();
        foreach (var p in products)
        {
            if (string.IsNullOrWhiteSpace(p.ImageUrl) || !ImageFileExists(p.ImageUrl))
            {
                p.ImageUrl = "/images/products/placeholder.svg";
            }

            if (!p.Images.Any(pi => pi.IsPrimary && ImageFileExists(pi.ImageUrl)))
            {
                p.Images.Add(new ProductImage
                {
                    ProductId = p.Id,
                    ImageUrl = p.ImageUrl,
                    IsPrimary = true
                });
            }
        }

        // 4. Yoga
        var yogas = await context.Yogas.ToListAsync();
        foreach (var y in yogas)
        {
            if (string.IsNullOrWhiteSpace(y.ImageUrl) || !ImageFileExists(y.ImageUrl))
            {
                if (y.YogaName.Contains("Surya", StringComparison.OrdinalIgnoreCase))
                    y.ImageUrl = "/images/yoga/surya-namaskar.svg";
                else if (y.YogaName.Contains("Vriksh", StringComparison.OrdinalIgnoreCase))
                    y.ImageUrl = "/images/yoga/vrikshasana.svg";
                else if (y.YogaName.Contains("Trikon", StringComparison.OrdinalIgnoreCase))
                    y.ImageUrl = "/images/yoga/trikonasana.svg";
                else
                    y.ImageUrl = "/images/yoga/pranayama.svg";
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Image repair completed: All category, doctor, product, and yoga images verified on disk.");
    }

    private static async Task SeedOrUpdateBannersAsync(AyurvedaDbContext context, ILogger logger)
    {
        var requiredBanners = new (string Title, string Subtitle, string ImageUrl, string LinkUrl, string Placement, int DisplayOrder)[]
        {
            // 6 Distinct Hero Banners (1920x640 WebP)
            ("Diwali Ojas Mahotsav", "Divine Radiance & Rejuvenation — Flat 30% Off on Classical Formulations & Gold Rasayanas", "/images/banners/banner-hero-diwali.webp", "/Shop?search=diwali", "home_hero", 1),
            ("Navratri Sattvic Shuddhi", "Kindle Digestive Agni & Pacify Pitta with Classical Churnas, Organic Vrat Ghee & Herbal Teas", "/images/banners/banner-hero-navratri.webp", "/Shop?search=navratri", "home_hero", 2),
            ("Dhanteras Arogya & Gift Hampers", "Bless Loved Ones with Authentic Wooden Chests, Pure Gir Cow Bilona Ghee & Shilajit Elixirs", "/images/banners/banner-hero-dhanteras.webp", "/Shop?category=Gift%20Hampers%20%26%20Wellness%20Sets", "home_hero", 3),
            ("Winter Immunity Shield", "Pacify Vata & Fortify Lungs — Wild Forest Amla Chyawanprash, Ginger Kadhas & Warm Body Oils", "/images/banners/banner-hero-winter.webp", "/Shop?category=Immunity%20%26%20Rasayana", "home_hero", 4),
            ("Free First Vaidya Consultation", "Discover Your Unique Prakriti & Imbalances with Senior BAMS Doctors via Private Video Call", "/images/banners/banner-hero-consult.webp", "/Doctors", "home_hero", 5),
            ("Sacred Herbal Teas & Yoga Harmony", "Infuse Your Sadhana with Handcrafted Whole-Leaf Botanical Brews, Tulsi Kadhas & Organic Cotton Mats", "/images/banners/banner-hero-teayoga.webp", "/Yoga", "home_hero", 6),

            // 4 Distinct Middle-of-Page Banners (1920x640 WebP)
            ("Authentic Panchakarma Home Kits", "Ancient Detox Tools: Pure Copper Neti Pots, Bronze Kansa Vatki Bowls & Massage Tailas", "/images/banners/banner-mid-panchakarma.webp", "/Shop?category=Panchakarma%20%26%20Therapy%20Kits", "home_middle", 1),
            ("Himalayan Wild Harvest & Raw Herbs", "Wild-Crafted Grade-A Shilajit, Ashwagandha Roots, Shatavari & Giloy from the Himalayas", "/images/banners/banner-mid-herbs.webp", "/Shop?category=Herbs%20%26%20Raw%20Powders", "home_middle", 2),
            ("Royal Kumkumadi & Ubtan Glow", "Infused with Kashmiri Saffron, Sandalwood & 24 Classical Herbs for Natural Radiance", "/images/banners/banner-mid-skinrituals.webp", "/Shop?category=Skin%20Care%20%26%20Complexion", "home_middle", 3),
            ("Daily Dinacharya Circadian Rituals", "Oil Pulling, Pure Copper Scrapers, Nasya Drops & Restful Brahmi Infusions", "/images/banners/banner-mid-dinacharya.webp", "/#dinacharya", "home_middle", 4)
        };

        var allBanners = await context.Banners.ToListAsync();
        foreach (var def in requiredBanners)
        {
            var existing = allBanners.FirstOrDefault(b => b.Title == def.Title)
                        ?? allBanners.FirstOrDefault(b => (b.Placement == def.Placement || (string.IsNullOrEmpty(b.Placement) && def.Placement == "home_hero")) && b.DisplayOrder == def.DisplayOrder);

            if (existing == null)
            {
                context.Banners.Add(new Banner
                {
                    Title = def.Title,
                    Subtitle = def.Subtitle,
                    ImageUrl = def.ImageUrl,
                    LinkUrl = def.LinkUrl,
                    Placement = def.Placement,
                    DisplayOrder = def.DisplayOrder,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.Title = def.Title;
                existing.Subtitle = def.Subtitle;
                existing.ImageUrl = def.ImageUrl;
                existing.LinkUrl = def.LinkUrl;
                existing.Placement = def.Placement;
                existing.DisplayOrder = def.DisplayOrder;
                existing.IsActive = true;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded and upserted 6 Hero and 4 Middle original WebP banners.");
    }

    private static async Task SeedFestivalOffersAsync(AyurvedaDbContext context, ILogger logger)
    {
        var requiredCoupons = new (string Code, DiscountType Type, decimal Amount, decimal MinOrder, decimal MaxDiscount, string Category)[]
        {
            ("DIWALI30", DiscountType.Percentage, 30m, 500m, 1000m, "All Categories"),
            ("NAVRATRI20", DiscountType.Percentage, 20m, 300m, 500m, "Digestive Care"),
            ("HEALTH15", DiscountType.Percentage, 15m, 250m, 300m, "All Categories"),
            ("VAIDYA100", DiscountType.Flat, 100m, 499m, 100m, "All Categories")
        };

        foreach (var def in requiredCoupons)
        {
            var existing = await context.Coupons.FirstOrDefaultAsync(c => c.Code == def.Code);
            if (existing == null)
            {
                context.Coupons.Add(new Coupon
                {
                    Code = def.Code,
                    DiscountType = def.Type,
                    DiscountAmount = def.Amount,
                    MinOrderAmount = def.MinOrder,
                    MaxDiscountAmount = def.MaxDiscount,
                    ApplicableCategory = def.Category,
                    StartDate = DateTime.UtcNow.AddDays(-10),
                    ExpiryDate = DateTime.UtcNow.AddDays(60),
                    UsageLimit = 5000,
                    UsageCount = 0,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.IsActive = true;
                existing.DiscountAmount = def.Amount;
                existing.MinOrderAmount = def.MinOrder;
                existing.MaxDiscountAmount = def.MaxDiscount;
                existing.ApplicableCategory = def.Category;
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded and verified Diwali & Navratri festival offers/coupons.");
    }
}
