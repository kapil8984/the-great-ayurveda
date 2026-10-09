using System.Text.Json;
using Ayurveda.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Ayurveda.API.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(AyurvedaDbContext context, bool isDevelopment = true, bool optInSeed = false)
    {
        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync();
        }

        // Rule e: Remove default passwords from seeding. Seed only in Development; production seeding must be opt-in.
        if (!isDevelopment && !optInSeed)
        {
            return;
        }

        // 1. Seed Admin (Development only)
        if (!await context.Admins.AnyAsync())
        {
            var admin = new Admin
            {
                AdminName = "Ayurveda Administrator",
                Email = "admin@ayurveda.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                ContactNumber = "9876543210",
                ProfileImage = "doctor.png",
                Role = "Admin",
                CreatedAt = DateTime.UtcNow
            };
            context.Admins.Add(admin);
            await context.SaveChangesAsync();
        }

        // 2. Seed Categories
        if (!await context.Categories.AnyAsync())
        {
            var categories = new List<Category>
            {
                new() { Name = "Digestive Health", Description = "Herbal formulas for digestion, metabolism, and gut health.", ImageUrl = "ayurvedic-bowl.png" },
                new() { Name = "Hair & Skin Care", Description = "Organic oils, cleansers, and herbal serums for natural radiance.", ImageUrl = "skin.png" },
                new() { Name = "Immunity & Rejuvenation", Description = "Rasayana herbs like Ashwagandha and Giloy for vitality.", ImageUrl = "human.png" },
                new() { Name = "Joint & Bone Care", Description = "Ancient anti-inflammatory formulations for flexible joints.", ImageUrl = "broken-bone.png" },
                new() { Name = "Mental Wellness", Description = "Brahmi and Shankhpushpi formulations for calm mind and sharp memory.", ImageUrl = "mental-health.png" },
                new() { Name = "Respiratory Health", Description = "Bronchial and lung care herbs for easy, clean breathing.", ImageUrl = "lungs.png" }
            };

            context.Categories.AddRange(categories);
            await context.SaveChangesAsync();
        }

        var digestiveCat = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Digestive Health");
        var skinCat = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Hair & Skin Care");
        var immunityCat = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Immunity & Rejuvenation");
        var jointCat = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Joint & Bone Care");
        var mentalCat = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Mental Wellness");
        var respCat = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Respiratory Health");

        // 3. Seed Users
        User? sampleUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "user@ayurveda.com");
        if (sampleUser == null)
        {
            sampleUser = new User
            {
                Username = "Aarav Sharma",
                Email = "user@ayurveda.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("User@123"),
                Contact = "9823456789",
                Role = "User",
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(sampleUser);
            await context.SaveChangesAsync();
        }

        // 4. Seed Doctors
        if (!await context.Doctors.AnyAsync())
        {
            var doctor1 = new Doctor
            {
                DoctorName = "Dr. Rajeshwar Sharma",
                Email = "dr.rajeshwar@ayurveda.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Doctor@123"),
                ContactNumber = "9811223344",
                ProfileImage = "1714299572533-678987792-handsome-indian-doctor-generate-ai-photo.jpg",
                Role = "Doctor",
                CreatedAt = DateTime.UtcNow
            };
            context.Doctors.Add(doctor1);
            await context.SaveChangesAsync();

            var detail1 = new DoctorDetail
            {
                DoctorId = doctor1.Id,
                QualificationImage = "1714144621467-13596409-doctorofmedicine-230509064515-8803e275-thumbnail.jpg",
                Experience = 14,
                Gender = "Male",
                Language = "Hindi, English, Sanskrit",
                ClinicAddress = "AyurVeda Wellness Center, MG Road, New Delhi",
                SpecializationId = digestiveCat?.Id ?? 1,
                Status = 1, // Approved
                MorningTimesJson = JsonSerializer.Serialize(new[] { "9:00 AM", "10:00 AM", "11:00 AM" }),
                AfternoonTimesJson = JsonSerializer.Serialize(new[] { "1:00 PM", "2:00 PM", "3:00 PM" }),
                EveningTimesJson = JsonSerializer.Serialize(new[] { "4:00 PM", "5:00 PM", "6:00 PM" })
            };
            context.DoctorDetails.Add(detail1);

            var doctor2 = new Doctor
            {
                DoctorName = "Dr. Ananya Joshi",
                Email = "dr.ananya@ayurveda.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Doctor@123"),
                ContactNumber = "9822334455",
                ProfileImage = "1714214500968-224476027-beautiful-young-female-doctor-looking-camera-office_1301-7807.avif",
                Role = "Doctor",
                CreatedAt = DateTime.UtcNow
            };
            context.Doctors.Add(doctor2);
            await context.SaveChangesAsync();

            var detail2 = new DoctorDetail
            {
                DoctorId = doctor2.Id,
                QualificationImage = "1714144621467-13596409-doctorofmedicine-230509064515-8803e275-thumbnail.jpg",
                Experience = 9,
                Gender = "Female",
                Language = "Hindi, English, Marathi",
                ClinicAddress = "Patanjali Shuddhi Clinic, Shivaji Park, Mumbai",
                SpecializationId = skinCat?.Id ?? 2,
                Status = 1, // Approved
                MorningTimesJson = JsonSerializer.Serialize(new[] { "10:00 AM", "11:00 AM" }),
                AfternoonTimesJson = JsonSerializer.Serialize(new[] { "2:00 PM", "3:00 PM" }),
                EveningTimesJson = JsonSerializer.Serialize(new[] { "5:00 PM", "6:00 PM" })
            };
            context.DoctorDetails.Add(detail2);

            // Pending doctor for admin verification showcase
            var doctor3 = new Doctor
            {
                DoctorName = "Dr. Vikramaditya Rathore",
                Email = "dr.vikram@ayurveda.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Doctor@123"),
                ContactNumber = "9833445566",
                ProfileImage = "1714367814418-139855071-a-mature-indian-male-doctor-on-a-white-background-ai-generated-photo.jpg",
                Role = "Doctor",
                CreatedAt = DateTime.UtcNow
            };
            context.Doctors.Add(doctor3);
            await context.SaveChangesAsync();

            var detail3 = new DoctorDetail
            {
                DoctorId = doctor3.Id,
                QualificationImage = "1714144621467-13596409-doctorofmedicine-230509064515-8803e275-thumbnail.jpg",
                Experience = 11,
                Gender = "Male",
                Language = "Hindi, English, Gujarati",
                ClinicAddress = "Charaka Samhita Ayurvedic Hospital, Ahmedabad",
                SpecializationId = jointCat?.Id ?? 4,
                Status = 0, // Pending
                MorningTimesJson = JsonSerializer.Serialize(new[] { "9:00 AM", "11:00 AM" }),
                AfternoonTimesJson = JsonSerializer.Serialize(new[] { "1:00 PM", "3:00 PM" }),
                EveningTimesJson = JsonSerializer.Serialize(new[] { "4:00 PM", "6:00 PM" })
            };
            context.DoctorDetails.Add(detail3);

            await context.SaveChangesAsync();
        }

        // 5. Seed Products
        if (!await context.Products.AnyAsync())
        {
            var products = new List<Product>
            {
                new()
                {
                    Title = "Triphala Churna Detox Formula",
                    Description = "Pure blend of Amalaki, Bibhitaki and Haritaki for digestive balance and daily cleansing.",
                    Price = 299.00m,
                    Stock = 85,
                    Brand = "The Great Ayurveda",
                    ImageUrl = "1714352284726-454156132-pro2.png",
                    CategoryId = digestiveCat?.Id ?? 1,
                    AverageRating = 4.8
                },
                new()
                {
                    Title = "Kumkumadi Tailam Miraculous Beauty Ayurvedic Oil",
                    Description = "Infused with pure Kashmiri saffron and 26 rare herbs for radiant, glowing skin and complexion.",
                    Price = 649.00m,
                    Stock = 60,
                    Brand = "The Great Ayurveda",
                    ImageUrl = "1714352353991-325392281-pro3.png",
                    CategoryId = skinCat?.Id ?? 2,
                    AverageRating = 4.9
                },
                new()
                {
                    Title = "Organic Ashwagandha Vitality Capsules",
                    Description = "Potent Withania Somnifera extract supporting stress resilience, muscular strength, and calm energy.",
                    Price = 449.00m,
                    Stock = 120,
                    Brand = "The Great Ayurveda",
                    ImageUrl = "1714352478007-509553923-pro4.png",
                    CategoryId = immunityCat?.Id ?? 3,
                    AverageRating = 4.7
                },
                new()
                {
                    Title = "Shallaki & Guggulu Joint Relief Tablets",
                    Description = "Traditional Ayurvedic resin blend promoting joint mobility, cartilage comfort, and flexibility.",
                    Price = 389.00m,
                    Stock = 45,
                    Brand = "The Great Ayurveda",
                    ImageUrl = "1714352488859-972066130-pro6.png",
                    CategoryId = jointCat?.Id ?? 4,
                    AverageRating = 4.6
                },
                new()
                {
                    Title = "Brahmi Mind & Focus Memory Elixir",
                    Description = "Medhya Rasayana herb promoting cognitive clarity, concentration, peaceful sleep, and memory retention.",
                    Price = 349.00m,
                    Stock = 70,
                    Brand = "The Great Ayurveda",
                    ImageUrl = "1714352500201-325537782-pro8.png",
                    CategoryId = mentalCat?.Id ?? 5,
                    AverageRating = 4.8
                },
                new()
                {
                    Title = "Vasaka & Tulsi Herbal Lung Syrup",
                    Description = "Soothing natural respiratory formula that clears bronchial pathways and supports easy breathing.",
                    Price = 249.00m,
                    Stock = 90,
                    Brand = "The Great Ayurveda",
                    ImageUrl = "1714352516965-266716696-pro11.png",
                    CategoryId = respCat?.Id ?? 6,
                    AverageRating = 4.5
                },
                new()
                {
                    Title = "Neem & Tea Tree Clarifying Body Lotion",
                    Description = "Gentle anti-microbial herbal moisturizer for soothing skin irritation and deep nourishment.",
                    Price = 379.00m,
                    Stock = 50,
                    Brand = "The Great Ayurveda",
                    ImageUrl = "1714782213289-96332174-1689411919Bodylotion100ml1.png",
                    CategoryId = skinCat?.Id ?? 2,
                    AverageRating = 4.7
                },
                new()
                {
                    Title = "Chyawanprash Awaleha 51-Herb Immunity Booster",
                    Description = "Amla-rich ancient elixir fortified with saffron and honey for whole-family vitality and protection.",
                    Price = 499.00m,
                    Stock = 110,
                    Brand = "The Great Ayurveda",
                    ImageUrl = "1714352528143-938011906-pro13.png",
                    CategoryId = immunityCat?.Id ?? 3,
                    AverageRating = 4.9
                }
            };

            context.Products.AddRange(products);
            await context.SaveChangesAsync();
        }

        // 6. Seed Yoga Poses
        if (!await context.Yogas.AnyAsync())
        {
            var yogas = new List<Yoga>
            {
                new()
                {
                    YogaName = "Surya Namaskar (Sun Salutation)",
                    Description = "A complete sequence of 12 gracefully linked asanas designed to awaken the body's solar energy, improve circulation, and activate all major muscle groups.",
                    Instructions = "Perform in the morning facing East. Flow through Pranamasana, Hastauttanasana, Padahastasana, Ashwa Sanchalanasana, Dandasana, Ashtanga Namaskara, and Bhujangasana with synchronized breath.",
                    ImageUrl = "https://images.unsplash.com/photo-1544367567-0f2fcb009e0b?auto=format&fit=crop&w=800&q=80",
                    VideoUrl = "https://www.youtube.com/watch?v=1xRX1MuoImw",
                    CategoryId = immunityCat?.Id ?? 3,
                    BenefitsJson = JsonSerializer.Serialize(new[]
                    {
                        "Stimulates all physiological systems including cardiovascular and endocrine",
                        "Improves spinal flexibility and posture",
                        "Boosts prana flow and mental alertness"
                    }),
                    AverageRating = 4.9
                },
                new()
                {
                    YogaName = "Bhujangasana (Cobra Pose)",
                    Description = "A classic back-bending posture that expands the chest, strengthens the spine, and invigorates the digestive organs.",
                    Instructions = "Lie prone with forehead on the mat. Place palms beneath the shoulders. Inhale slowly and lift the chest off the floor, engaging spinal extensors without compressing the lumbar spine.",
                    ImageUrl = "https://images.unsplash.com/photo-1506126613408-eca07ce68773?auto=format&fit=crop&w=800&q=80",
                    VideoUrl = "https://www.youtube.com/watch?v=fOdrW7nf9gw",
                    CategoryId = respCat?.Id ?? 6,
                    BenefitsJson = JsonSerializer.Serialize(new[]
                    {
                        "Expands chest cavity and deepens lung capacity",
                        "Alleviates mild back stiffness and sciatica",
                        "Stimulates abdominal organs and kidney function"
                    }),
                    AverageRating = 4.7
                },
                new()
                {
                    YogaName = "Vrikshasana (Tree Pose)",
                    Description = "A grounding balancing pose that cultivates unwavering focus, neuromuscular coordination, and mental equilibrium.",
                    Instructions = "Stand tall in Tadasana. Shift weight to left leg, place right sole on inner left thigh (avoiding the knee). Bring palms to Anjali Mudra at the heart or extend overhead.",
                    ImageUrl = "https://images.unsplash.com/photo-1518611012118-696072aa579a?auto=format&fit=crop&w=800&q=80",
                    VideoUrl = "https://www.youtube.com/watch?v=wdln9qWYloU",
                    CategoryId = mentalCat?.Id ?? 5,
                    BenefitsJson = JsonSerializer.Serialize(new[]
                    {
                        "Calms the nervous system and relieves mental fatigue",
                        "Strengthens ankles, calves, and deep core stabilizers",
                        "Enhances proprioception and equilibrium"
                    }),
                    AverageRating = 4.8
                },
                new()
                {
                    YogaName = "Pawanmuktasana (Wind-Relieving Pose)",
                    Description = "An essential digestive asana that gently compresses the abdominal viscera, helping expel trapped intestinal gases and stimulate peristalsis.",
                    Instructions = "Lie supine, bend both knees toward the chest, interlace fingers around the shins. Inhale deeply, and upon exhalation gently bring forehead toward knees.",
                    ImageUrl = "https://images.unsplash.com/photo-1575052814086-f385e2e2ad1b?auto=format&fit=crop&w=800&q=80",
                    VideoUrl = "https://www.youtube.com/watch?v=2n3AkyF7qY8",
                    CategoryId = digestiveCat?.Id ?? 1,
                    BenefitsJson = JsonSerializer.Serialize(new[]
                    {
                        "Relieves digestive sluggishness, gas, and bloating",
                        "Tones abdominal muscles and internal organs",
                        "Relaxes lower back tension"
                    }),
                    AverageRating = 4.6
                }
            };

            context.Yogas.AddRange(yogas);
            await context.SaveChangesAsync();
        }
    }
}
