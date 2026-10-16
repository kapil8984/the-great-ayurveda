using Microsoft.Extensions.Caching.Memory;
using Ayurveda.Application.Interfaces;

namespace Ayurveda.Infrastructure.Services;

public class CacheBuster : ICacheBuster
{
    private readonly IMemoryCache _cache;

    public CacheBuster(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void InvalidateProduct(int? productId = null)
    {
        _cache.Remove("Shop_Default_Page1");
        _cache.Remove("Shop_Brands_Filters");
        if (productId.HasValue)
        {
            _cache.Remove($"Product_Details_Base_{productId.Value}");
        }
        if (_cache is MemoryCache mc)
        {
            mc.Clear();
        }
    }

    public void InvalidateCategory()
    {
        _cache.Remove("Shop_Default_Page1");
        _cache.Remove("Shop_Categories_Filters");
        _cache.Remove("Shop_Subcategories_Filters");
        if (_cache is MemoryCache mc)
        {
            mc.Clear();
        }
    }

    public void InvalidateBanner()
    {
        _cache.Remove("Shop_Banners");
        _cache.Remove("Doctor_Banners");
        _cache.Remove("Cart_Banners");
        if (_cache is MemoryCache mc)
        {
            mc.Clear();
        }
    }

    public void InvalidateDoctor()
    {
        _cache.Remove("Doctors_Default_List");
        _cache.Remove("Doctors_Specializations");
        _cache.Remove("Doctor_Banners");
        if (_cache is MemoryCache mc)
        {
            mc.Clear();
        }
    }

    public void InvalidateCoupon()
    {
        if (_cache is MemoryCache mc)
        {
            mc.Clear();
        }
    }

    public void ClearAllStorefrontCaches()
    {
        if (_cache is MemoryCache mc)
        {
            mc.Clear();
        }
        else
        {
            _cache.Remove("Shop_Default_Page1");
            _cache.Remove("Shop_Categories_Filters");
            _cache.Remove("Shop_Subcategories_Filters");
            _cache.Remove("Shop_Brands_Filters");
            _cache.Remove("Shop_Banners");
            _cache.Remove("Doctors_Default_List");
            _cache.Remove("Doctors_Specializations");
            _cache.Remove("Doctor_Banners");
            _cache.Remove("Cart_Banners");
        }
    }
}
