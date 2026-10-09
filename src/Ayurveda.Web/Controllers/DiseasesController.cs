using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ayurveda.Application.ViewModels;
using Ayurveda.Infrastructure.Data;

namespace Ayurveda.Web.Controllers;

public class DiseasesController : Controller
{
    private readonly AyurvedaDbContext _context;

    public DiseasesController(AyurvedaDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? query)
    {
        var allRemedies = new List<HomeRemedyItemViewModel>
        {
            new()
            {
                ConcernTitle = "Digestive Sluggishness & Bloating",
                SanskritName = "Agni Mandya",
                TraditionalDescription = "A weakened digestive fire (Agni) leading to heaviness after meals, mild gas, and sluggish metabolic transformation.",
                TraditionalRemedies = new()
                {
                    "Drink warm water infused with dry ginger powder and cumin seeds before meals",
                    "Chew a slice of fresh ginger with a pinch of rock salt 10 minutes prior to eating",
                    "Sip freshly churned buttermilk with roasted cumin and mint following lunch"
                },
                DietaryAdvice = new() { "Avoid ice-cold beverages with food", "Favor freshly prepared warm soups and khichdi", "Avoid excessive raw heavy salads at night" },
                SuitableHerbs = new() { "Triphala", "Trikatu", "Ajwain", "Hing", "Cumin" }
            },
            new()
            {
                ConcernTitle = "Seasonal Vitality & Immunity",
                SanskritName = "Ojas Kshaya",
                TraditionalDescription = "Depleted natural vitality (Ojas) manifesting as seasonal vulnerability, fatigue, and sluggish recovery.",
                TraditionalRemedies = new()
                {
                    "Consume one spoonful of authentic Amla Chyawanprash in the morning with warm milk",
                    "Drink freshly brewed Tulsi and black pepper herbal infusion once daily",
                    "Practice 15 minutes of Anulom Vilom pranayama to enhance prana flow"
                },
                DietaryAdvice = new() { "Include soaked almonds and dates in breakfast", "Favor golden milk with turmeric and pinch of black pepper at bedtime" },
                SuitableHerbs = new() { "Amalaki", "Giloy (Guduchi)", "Ashwagandha", "Tulsi" }
            },
            new()
            {
                ConcernTitle = "Joint & Muscle Stiffness",
                SanskritName = "Sandhivata Care",
                TraditionalDescription = "Aggravated dry, cold Vata dosha accumulating in the joints and connective tissues, resulting in morning stiffness.",
                TraditionalRemedies = new()
                {
                    "Apply warm Mahanarayan or sesame oil with gentle, circular strokes on affected joints",
                    "Take a warm water bath or apply mild herbal fomentation after oil application",
                    "Consume golden turmeric milk with a pinch of nutmeg before sleep"
                },
                DietaryAdvice = new() { "Favor warm cooked grains with pure cow ghee", "Reduce dry, cold snacks, raw foods, and carbonated beverages" },
                SuitableHerbs = new() { "Shallaki (Boswellia)", "Guggulu", "Dashamula", "Ashwagandha" }
            },
            new()
            {
                ConcernTitle = "Stress, Restless Mind & Sleep Ease",
                SanskritName = "Manovaha Strotas Support",
                TraditionalDescription = "Hyperactive Vata and Pitta affecting mental serenity, resulting in scattered focus, restlessness, and disturbed sleep.",
                TraditionalRemedies = new()
                {
                    "Gently massage the soles of the feet with warm sesame oil (Pada Abhyanga) before sleeping",
                    "Drink warm chamomile or Brahmi tea in the evening",
                    "Practice Shavasana and slow abdominal breathing for 10 minutes before bed"
                },
                DietaryAdvice = new() { "Favor warm, easily digestible light dinners before 8 PM", "Avoid screen time and caffeine after 6 PM" },
                SuitableHerbs = new() { "Ashwagandha", "Brahmi (Bacopa)", "Jatamansi", "Shankhpushpi" }
            }
        };

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim().ToLower();
            allRemedies = allRemedies.Where(r => r.ConcernTitle.ToLower().Contains(q) ||
                                                 r.SanskritName.ToLower().Contains(q) ||
                                                 r.TraditionalDescription.ToLower().Contains(q) ||
                                                 r.SuitableHerbs.Any(h => h.ToLower().Contains(q))).ToList();
        }

        var model = new DiseaseSearchViewModel
        {
            Query = query,
            Remedies = allRemedies
        };

        return View(model);
    }
}
