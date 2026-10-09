import React, { useEffect, useState } from "react";
import axios from "axios";
import Api from "../WebApi/Api";
import { toast } from "react-toastify";
import starGenerate from "./GenerateRating";
import { useNavigate, useLocation } from "react-router-dom";
import { FaSearch, FaShoppingBag, FaBolt, FaBoxOpen } from "react-icons/fa";

function ShowProductToUser() {
    const navigate = useNavigate();
    const location = useLocation();

    const [products, setProducts] = useState([]);
    const [categories, setCategories] = useState([]);
    const [loading, setLoading] = useState(true);

    // Filters and controls
    const [searchTerm, setSearchTerm] = useState("");
    const [selectedCategory, setSelectedCategory] = useState("All");
    const [selectedPriceOrder, setSelectedPriceOrder] = useState("default");
    const [minRating, setMinRating] = useState(0);
    const [maxPrice, setMaxPrice] = useState(5000);
    const [currentPage, setCurrentPage] = useState(1);
    const itemsPerPage = 8;

    const currentUser = JSON.parse(sessionStorage.getItem('user-details') || 'null');

    useEffect(() => {
        loadData();
    }, []);

    useEffect(() => {
        if (location.state?.categoryId) {
            setSelectedCategory(location.state.categoryId);
        }
    }, [location.state]);

    const loadData = async () => {
        try {
            setLoading(true);
            const [prodsRes, catsRes] = await Promise.allSettled([
                axios.get(Api.getAllProduct),
                axios.get(Api.getCategory)
            ]);

            if (prodsRes.status === "fulfilled") {
                const list = prodsRes.value.data.product || prodsRes.value.data.products || [];
                setProducts(list);
            }

            if (catsRes.status === "fulfilled") {
                const catList = catsRes.value.data.categories || catsRes.value.data.category || [];
                setCategories(catList);
            }
        } catch (error) {
            console.error("Error loading products:", error);
            toast.error("Failed to load products");
        } finally {
            setLoading(false);
        }
    };

    const handleAddToCart = async (product) => {
        if (!currentUser) {
            toast.info("Please sign in to add products to your cart");
            navigate('/user-account');
            return;
        }

        if (product.stock <= 0) {
            toast.warning("This item is currently out of stock");
            return;
        }

        try {
            const prodId = product._id || product.id;
            const userId = currentUser._id || currentUser.id;
            const response = await axios.post(Api.addToCart, { productId: prodId, userId });
            toast.success(response.data.message || "Added to cart!");
        } catch (error) {
            console.error(error);
            toast.error("Could not add item to cart");
        }
    };

    const handleBuyNow = async (product) => {
        if (!currentUser) {
            toast.info("Please sign in to continue");
            navigate('/user-account');
            return;
        }
        await handleAddToCart(product);
        navigate('/show-cart');
    };

    // Filter pipeline
    const filteredProducts = products.filter((prod) => {
        const titleMatch = prod.title?.toLowerCase().includes(searchTerm.toLowerCase()) || false;
        const brandMatch = prod.brand?.toLowerCase().includes(searchTerm.toLowerCase()) || false;
        const descMatch = prod.description?.toLowerCase().includes(searchTerm.toLowerCase()) || false;
        const matchesSearch = titleMatch || brandMatch || descMatch;

        const prodCatId = prod.category?._id || prod.category?.id || prod.categoryId || "";
        const matchesCategory = selectedCategory === "All" || prodCatId === selectedCategory;

        const matchesRating = (prod.averageRating || 0) >= minRating;
        const matchesPrice = (prod.price || 0) <= maxPrice;

        return matchesSearch && matchesCategory && matchesRating && matchesPrice;
    });

    // Sort pipeline
    const sortedProducts = [...filteredProducts].sort((a, b) => {
        if (selectedPriceOrder === "low-to-high") return a.price - b.price;
        if (selectedPriceOrder === "high-to-low") return b.price - a.price;
        if (selectedPriceOrder === "rating") return (b.averageRating || 0) - (a.averageRating || 0);
        return 0;
    });

    // Pagination pipeline
    const totalPages = Math.ceil(sortedProducts.length / itemsPerPage);
    const displayedProducts = sortedProducts.slice(
        (currentPage - 1) * itemsPerPage,
        currentPage * itemsPerPage
    );

    return (
        <div className="container-fluid py-5 px-3 px-md-5">
            {/* Header */}
            <div className="text-center mb-4">
                <span className="badge bg-success-subtle text-success px-3 py-2 rounded-pill fw-semibold mb-2">
                    Pure Ayurvedic Pharmacy
                </span>
                <h1 className="display-6 fw-bold font-playfair text-dark mb-1">Authentic Herbal Formulations</h1>
                <p className="text-muted mx-auto small" style={{ maxWidth: '600px' }}>
                    Prepared using ancient classical texts (Charaka & Sushruta Samhita) with organically cultivated medicinal herbs.
                </p>
            </div>

            {/* Filter & Search Bar */}
            <div className="card border-0 shadow-sm rounded-4 p-4 mb-4 bg-white">
                <div className="row g-3 align-items-center">
                    {/* Search */}
                    <div className="col-12 col-md-4">
                        <div className="input-group rounded-pill overflow-hidden border">
                            <span className="input-group-text bg-white border-0 ps-3 text-muted">
                                <FaSearch />
                            </span>
                            <input
                                type="text"
                                className="form-control border-0 py-2"
                                placeholder="Search products, brands, or herbs..."
                                value={searchTerm}
                                onChange={(e) => { setSearchTerm(e.target.value); setCurrentPage(1); }}
                            />
                        </div>
                    </div>

                    {/* Category Filter */}
                    <div className="col-6 col-md-3">
                        <select
                            className="form-select rounded-pill py-2"
                            value={selectedCategory}
                            onChange={(e) => { setSelectedCategory(e.target.value); setCurrentPage(1); }}
                        >
                            <option value="All">All Categories</option>
                            {categories.map((c) => (
                                <option key={c._id || c.id} value={c._id || c.id}>
                                    {c.name}
                                </option>
                            ))}
                        </select>
                    </div>

                    {/* Sort Order */}
                    <div className="col-6 col-md-3">
                        <select
                            className="form-select rounded-pill py-2"
                            value={selectedPriceOrder}
                            onChange={(e) => setSelectedPriceOrder(e.target.value)}
                        >
                            <option value="default">Sort: Default</option>
                            <option value="low-to-high">Price: Low to High</option>
                            <option value="high-to-low">Price: High to Low</option>
                            <option value="rating">Highest Rated</option>
                        </select>
                    </div>

                    {/* Rating Filter */}
                    <div className="col-6 col-md-1">
                        <select
                            className="form-select rounded-pill py-2"
                            value={minRating}
                            onChange={(e) => { setMinRating(Number(e.target.value)); setCurrentPage(1); }}
                        >
                            <option value="0">Rating</option>
                            <option value="4">4★+</option>
                            <option value="3">3★+</option>
                        </select>
                    </div>

                    {/* Max Price Filter */}
                    <div className="col-6 col-md-1">
                        <select
                            className="form-select rounded-pill py-2"
                            value={maxPrice}
                            onChange={(e) => { setMaxPrice(Number(e.target.value)); setCurrentPage(1); }}
                        >
                            <option value="5000">Price</option>
                            <option value="200">≤₹200</option>
                            <option value="500">≤₹500</option>
                            <option value="1000">≤₹1k</option>
                        </select>
                    </div>
                </div>

                {/* Active Filter Chips / Reset */}
                <div className="d-flex justify-content-between align-items-center mt-3 pt-3 border-top small text-muted">
                    <span>
                        Showing <strong>{sortedProducts.length}</strong> herbal medicines
                    </span>
                    {(searchTerm || selectedCategory !== "All" || selectedPriceOrder !== "default" || minRating > 0) && (
                        <button
                            className="btn btn-link btn-sm text-danger text-decoration-none p-0 fw-semibold"
                            onClick={() => {
                                setSearchTerm("");
                                setSelectedCategory("All");
                                setSelectedPriceOrder("default");
                                setMinRating(0);
                                setCurrentPage(1);
                            }}
                        >
                            Clear All Filters
                        </button>
                    )}
                </div>
            </div>

            {/* Products Grid */}
            {loading ? (
                <div className="d-flex justify-content-center align-items-center py-5">
                    <div className="spinner-border text-success" style={{ width: '3rem', height: '3rem' }} role="status">
                        <span className="visually-hidden">Loading Ayurvedic medicines...</span>
                    </div>
                </div>
            ) : sortedProducts.length === 0 ? (
                <div className="text-center py-5 card border-0 shadow-sm rounded-4 p-5 mx-auto" style={{ maxWidth: '500px' }}>
                    <FaBoxOpen size={50} className="text-muted mx-auto mb-3" />
                    <h5 className="fw-semibold text-secondary">No products found</h5>
                    <p className="text-muted small">Try modifying your search or removing selected filters.</p>
                </div>
            ) : (
                <>
                    <div className="row g-4">
                        {displayedProducts.map((product, index) => {
                            const imgSrc = product.imageURL
                                ? `${Api.imageBaseUrl}${product.imageURL}`
                                : 'https://i.ibb.co/ZNB6jDz/ayurvedic-bowl.png';
                            const inStock = product.stock > 0;
                            const isLowStock = product.stock <= 5 && inStock;

                            return (
                                <div className="col-12 col-sm-6 col-lg-3" key={product._id || product.id || index}>
                                    <div className="card h-100 border-0 shadow-sm rounded-4 overflow-hidden card-ayur d-flex flex-column transition-all">
                                        {/* Image Box */}
                                        <div
                                            className="position-relative bg-white p-3 text-center cursor-pointer"
                                            onClick={() => navigate('/show-product-details', { state: { product } })}
                                            style={{ cursor: 'pointer' }}
                                        >
                                            <img
                                                src={imgSrc}
                                                alt={product.title}
                                                className="img-fluid object-fit-contain"
                                                style={{ height: '200px' }}
                                                onError={(e) => { e.target.onerror = null; e.target.src = 'https://i.ibb.co/ZNB6jDz/ayurvedic-bowl.png'; }}
                                            />
                                            {/* Stock Badge */}
                                            {isLowStock ? (
                                                <span className="badge bg-warning text-dark position-absolute top-0 end-0 m-2 rounded-pill small">
                                                    Only {product.stock} Left
                                                </span>
                                            ) : !inStock ? (
                                                <span className="badge bg-danger position-absolute top-0 end-0 m-2 rounded-pill small">
                                                    Out of Stock
                                                </span>
                                            ) : (
                                                <span className="badge bg-success-subtle text-success position-absolute top-0 end-0 m-2 rounded-pill small fw-semibold">
                                                    In Stock
                                                </span>
                                            )}
                                        </div>

                                        {/* Content Box */}
                                        <div className="card-body p-3 d-flex flex-column flex-grow-1">
                                            <div className="d-flex justify-content-between align-items-center mb-1">
                                                <small className="text-muted text-uppercase fw-semibold" style={{ fontSize: '11px' }}>
                                                    {product.brand || 'Vaidya Choice'}
                                                </small>
                                                {product.category && (
                                                    <span className="badge bg-light text-secondary border small" style={{ fontSize: '10px' }}>
                                                        {product.category.name}
                                                    </span>
                                                )}
                                            </div>

                                            <h6
                                                className="fw-bold text-dark font-playfair mb-1 text-truncate cursor-pointer"
                                                onClick={() => navigate('/show-product-details', { state: { product } })}
                                                title={product.title}
                                                style={{ cursor: 'pointer' }}
                                            >
                                                {product.title}
                                            </h6>

                                            <p className="text-muted small mb-2" style={{ height: '36px', overflow: 'hidden' }}>
                                                {product.description}
                                            </p>

                                            <div className="mb-2">
                                                {starGenerate(product.averageRating || 5)}
                                            </div>

                                            <div className="d-flex justify-content-between align-items-center mt-auto pt-3 border-top">
                                                <div>
                                                    <span className="fs-5 fw-bold text-success">
                                                        ₹ {product.price}
                                                    </span>
                                                </div>

                                                <div className="d-flex gap-2">
                                                    <button
                                                        onClick={() => handleAddToCart(product)}
                                                        className="btn btn-sm btn-primary-ayur text-white rounded-pill px-3 d-flex align-items-center gap-1 shadow-sm"
                                                        disabled={!inStock}
                                                        title="Add to cart"
                                                    >
                                                        <FaShoppingBag size={12} /> Add
                                                    </button>
                                                    <button
                                                        onClick={() => handleBuyNow(product)}
                                                        className="btn btn-sm btn-outline-warning text-dark rounded-pill px-2 d-flex align-items-center gap-1"
                                                        disabled={!inStock}
                                                        title="Buy immediately"
                                                    >
                                                        <FaBolt size={11} /> Buy
                                                    </button>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            );
                        })}
                    </div>

                    {/* Pagination */}
                    {totalPages > 1 && (
                        <div className="d-flex justify-content-center align-items-center gap-2 mt-5">
                            <button
                                className="btn btn-outline-secondary rounded-pill px-3 btn-sm"
                                onClick={() => setCurrentPage((p) => Math.max(p - 1, 1))}
                                disabled={currentPage === 1}
                            >
                                Previous
                            </button>
                            <span className="mx-2 small text-muted">
                                Page <strong>{currentPage}</strong> of <strong>{totalPages}</strong>
                            </span>
                            <button
                                className="btn btn-outline-secondary rounded-pill px-3 btn-sm"
                                onClick={() => setCurrentPage((p) => Math.min(p + 1, totalPages))}
                                disabled={currentPage === totalPages}
                            >
                                Next
                            </button>
                        </div>
                    )}
                </>
            )}
        </div>
    );
}

export default ShowProductToUser;
