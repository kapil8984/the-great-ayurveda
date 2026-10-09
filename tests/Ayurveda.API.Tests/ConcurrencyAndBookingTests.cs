using System.Net;
using System.Net.Http.Json;
using Ayurveda.API.Data;
using Ayurveda.API.DTOs;
using Ayurveda.API.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ayurveda.API.Tests;

public class ConcurrencyAndBookingTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ConcurrencyAndBookingTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AppointmentDoubleBooking_DuplicateSlot_ReturnsFriendlyErrorMessage()
    {
        // Seed doctor and patient
        int doctorId = 201;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();
            if (!db.Doctors.Any(d => d.Id == doctorId))
            {
                var doc = new Doctor
                {
                    Id = doctorId,
                    DoctorName = "Dr. Concurrency",
                    Email = "dr.concurrency@ayurveda.com",
                    PasswordHash = "hashed",
                    ContactNumber = "9988776655"
                };
                db.Doctors.Add(doc);

                db.DoctorDetails.Add(new DoctorDetail
                {
                    DoctorId = doctorId,
                    Status = 1,
                    Experience = 10,
                    Gender = "Male",
                    Language = "English",
                    ClinicAddress = "Clinic A"
                });

                if (!db.Users.Any(u => u.Id == 1))
                {
                    db.Users.Add(new User { Id = 1, Username = "Patient One", Email = "p1@test.com", PasswordHash = "h" });
                }
                if (!db.Users.Any(u => u.Id == 2))
                {
                    db.Users.Add(new User { Id = 2, Username = "Patient Two", Email = "p2@test.com", PasswordHash = "h" });
                }

                await db.SaveChangesAsync();
            }
        }

        var clientPatient1 = _factory.CreateAuthenticatedClient(role: "User", id: 1, email: "p1@test.com", name: "Patient One");
        var clientPatient2 = _factory.CreateAuthenticatedClient(role: "User", id: 2, email: "p2@test.com", name: "Patient Two");

        var bookingRequest = new
        {
            doctorId = doctorId,
            appointmentDate = DateTime.UtcNow.Date.AddDays(3),
            timeSlot = "10:00 AM",
            patientName = "Test Patient",
            patientContact = "9988776655",
            healthConcern = "Digestive issue"
        };

        // Patient 1 books slot
        var resp1 = await clientPatient1.PostAsJsonAsync("/api/appointments", bookingRequest);
        Assert.Equal(HttpStatusCode.Created, resp1.StatusCode);

        // Patient 2 attempts booking the exact same slot for the same doctor
        var resp2 = await clientPatient2.PostAsJsonAsync("/api/appointments", bookingRequest);
        Assert.Equal(HttpStatusCode.BadRequest, resp2.StatusCode);

        var content2 = await resp2.Content.ReadAsStringAsync();
        Assert.Contains("already booked", content2, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OrderCreation_ExceedsStock_FailsSafely()
    {
        int productId = 301;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();
            if (!db.Products.Any(p => p.Id == productId))
            {
                var cat = new Category { Id = 301, Name = "StockCat", Description = "Desc" };
                db.Categories.Add(cat);
                db.Products.Add(new Product
                {
                    Id = productId,
                    Title = "Limited Stock Tonic",
                    Price = 250,
                    Stock = 3, // only 3 available
                    CategoryId = 301
                });
                await db.SaveChangesAsync();
            }
        }

        var client = _factory.CreateAuthenticatedClient(role: "User", id: 1, email: "buyer@test.com");

        var orderRequest = new
        {
            products = new[]
            {
                new { productId = productId, quantity = 5 } // request 5 when only 3 available
            },
            shippingAddress = "123 Street",
            paymentMethod = "CashOnDelivery"
        };

        var response = await client.PostAsJsonAsync("/api/orders", orderRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Insufficient stock", content, StringComparison.OrdinalIgnoreCase);

        // Verify stock was not decremented
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AyurvedaDbContext>();
            var p = await db.Products.FindAsync(productId);
            Assert.Equal(3, p!.Stock);
        }
    }
}
