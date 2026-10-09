import React, { useEffect, useState } from "react";
import { Link, useNavigate, useLocation } from "react-router-dom";
import { toast } from "react-toastify";
import Api, { apiClient } from "./WebApi/Api";
import { 
  FaUser, 
  FaShoppingCart, 
  FaSignOutAlt, 
  FaSearch, 
  FaBars, 
  FaTimes, 
  FaUserMd, 
  FaLeaf, 
  FaBoxOpen,
  FaShieldAlt,
  FaPhoneAlt,
  FaSpa,
  FaCalendarCheck
} from "react-icons/fa";

function Header() {
  const [searchValue, setSearchValue] = useState('');
  const [showSuggestions, setShowSuggestions] = useState(false);
  const [categories, setCategories] = useState([]);
  const [products, setProducts] = useState([]);
  const [filteredSuggestions, setFilteredSuggestions] = useState([]);
  
  const [user, setUser] = useState(null);
  const [doctor, setDoctor] = useState(null);
  const [admin, setAdmin] = useState(null);
  const [mobileDrawerOpen, setMobileDrawerOpen] = useState(false);
  const [cartCount, setCartCount] = useState(0);

  const navigate = useNavigate();
  const location = useLocation();

  useEffect(() => {
    // Check authenticated sessions
    const userData = sessionStorage.getItem('user-details');
    if (userData) {
      try { setUser(JSON.parse(userData)); } catch (e) { setUser(null); }
    } else {
      setUser(null);
    }

    const docData = sessionStorage.getItem('doctor-details');
    if (docData) {
      try { setDoctor(JSON.parse(docData)); } catch (e) { setDoctor(null); }
    } else {
      setDoctor(null);
    }

    const adminData = sessionStorage.getItem('admin-details');
    if (adminData) {
      try { setAdmin(JSON.parse(adminData)); } catch (e) { setAdmin(null); }
    } else {
      setAdmin(null);
    }

    loadCategoriesAndProducts();
    loadCartCount();
  }, [location.pathname]);

  const loadCategoriesAndProducts = async () => {
    try {
      const [catRes, prodRes] = await Promise.allSettled([
        apiClient.get(Api.getCategory),
        apiClient.get(Api.getAllProduct)
      ]);

      if (catRes.status === "fulfilled") {
        setCategories(catRes.value.data.categories || catRes.value.data.category || []);
      }
      if (prodRes.status === "fulfilled") {
        setProducts(prodRes.value.data.product || prodRes.value.data.products || []);
      }
    } catch (err) {
      console.error("Error loading header suggestions", err);
    }
  };

  const loadCartCount = async () => {
    const userData = sessionStorage.getItem('user-details');
    if (!userData) {
      setCartCount(0);
      return;
    }
    try {
      const u = JSON.parse(userData);
      const res = await apiClient.post(Api.getCart, { userId: u.id || u._id });
      if (res.data?.cart?.cartItems) {
        setCartCount(res.data.cart.cartItems.length);
      }
    } catch (err) {
      // ignore
    }
  };

  // Instant autocomplete filtering
  useEffect(() => {
    if (!searchValue.trim()) {
      setFilteredSuggestions([]);
      return;
    }
    const q = searchValue.toLowerCase();
    
    const catMatches = categories
      .filter(c => c.name.toLowerCase().includes(q))
      .slice(0, 3)
      .map(c => ({ type: 'Category', label: c.name, id: c.id || c._id }));

    const prodMatches = products
      .filter(p => p.title.toLowerCase().includes(q) || (p.brand && p.brand.toLowerCase().includes(q)))
      .slice(0, 4)
      .map(p => ({ type: 'Product', label: p.title, item: p }));

    setFilteredSuggestions([...catMatches, ...prodMatches]);
  }, [searchValue, categories, products]);

  const handleSearchSubmit = (e) => {
    if (e) e.preventDefault();
    if (searchValue.trim()) {
      setShowSuggestions(false);
      navigate('/get-all-products', { state: { searchTerm: searchValue.trim() } });
    }
  };

  const handleSuggestionSelect = (sugg) => {
    setShowSuggestions(false);
    setSearchValue('');
    if (sugg.type === 'Category') {
      navigate('/get-all-products', { state: { categoryName: sugg.label, categoryId: sugg.id } });
    } else if (sugg.type === 'Product') {
      navigate('/show-product-details', { state: { product: sugg.item } });
    }
  };

  const handleLogOut = () => {
    sessionStorage.clear();
    setUser(null);
    setDoctor(null);
    setAdmin(null);
    setCartCount(0);
    toast.info('Logged out successfully');
    navigate('/');
  };

  const handleCartClick = () => {
    if (!user) {
      toast.warning('Please login as user to view your cart');
      navigate('/user-account');
      return;
    }
    navigate('/show-cart');
  };

  const brandLogo = 'https://i.postimg.cc/0N4mvQzt/png-transparent-ayurveda-healing-tree-art-ayurveda-therapy-medicine-hospital-panchakarma-herbal-food.png';

  return (
    <>
      {/* 1. Announcement Bar */}
      <div className="ayur-announcement-bar d-none d-sm-block">
        <div className="container-fluid px-lg-5 px-3">
          <div className="d-flex justify-content-between align-items-center">
            <div className="d-flex align-items-center gap-3">
              <span>🌿 <strong>Authentic Classical Formulations</strong> • 100% Certified BAMS Vaidyas</span>
              <span className="d-none d-md-inline opacity-75">|</span>
              <span className="d-none d-md-inline">Free Delivery on Orders Over ₹999</span>
            </div>
            <div className="d-flex align-items-center gap-3">
              <span className="d-flex align-items-center gap-1">
                <FaPhoneAlt size={11} className="text-warning" /> Helpline: <strong>1800-AYUR-VEDA</strong>
              </span>
              <Link to="/doctorconsult" className="text-warning fw-semibold text-decoration-none">
                Consult a Doctor →
              </Link>
            </div>
          </div>
        </div>
      </div>

      {/* 2. Top Header Bar */}
      <header className="ayur-topheader">
        <div className="container-fluid px-lg-5 px-3">
          <div className="d-flex justify-content-between align-items-center gap-3">
            
            {/* Brand Logo & Name */}
            <Link to="/" className="d-flex align-items-center text-decoration-none flex-shrink-0">
              <img 
                src={brandLogo} 
                alt="The Great Ayurveda Logo" 
                style={{ height: '48px', width: '48px', borderRadius: '50%', objectFit: 'cover' }}
                className="me-2 shadow-sm border border-success border-opacity-25"
                onError={(e) => { e.target.style.display = 'none'; }}
              />
              <div>
                <span className="brand-title fs-5 d-block mb-0" style={{ letterSpacing: '0.04em', color: '#0f2c1d' }}>
                  THE GREAT AYURVEDA
                </span>
                <small className="text-muted d-none d-sm-block" style={{ fontSize: '0.72rem', letterSpacing: '0.08em', fontWeight: 600 }}>
                  SACRED HEALTHCARE & HERBAL WELLNESS
                </small>
              </div>
            </Link>

            {/* Desktop Search Bar with Autocomplete Suggestions */}
            <div className="d-none d-md-block position-relative flex-grow-1" style={{ maxWidth: '460px' }}>
              <form onSubmit={handleSearchSubmit} className="d-flex align-items-center position-relative">
                <input
                  type="search"
                  className="ayur-search-input pe-5"
                  placeholder="Search remedies, herbs, doshas, products..."
                  value={searchValue}
                  onChange={(e) => {
                    setSearchValue(e.target.value);
                    setShowSuggestions(true);
                  }}
                  onFocus={() => setShowSuggestions(true)}
                  aria-label="Search remedies and herbs"
                />
                <button 
                  type="submit" 
                  className="ayur-search-btn position-absolute" 
                  style={{ right: '4px', top: '4px', bottom: '4px' }}
                  aria-label="Submit search"
                >
                  <FaSearch size={13} />
                </button>
              </form>

              {/* Suggestions Dropdown */}
              {showSuggestions && filteredSuggestions.length > 0 && (
                <div 
                  className="position-absolute bg-white border rounded-3 shadow-lg mt-1 w-100 overflow-hidden" 
                  style={{ zIndex: 1060 }}
                >
                  <div className="p-2 bg-light border-bottom text-muted small fw-semibold d-flex justify-content-between">
                    <span>Quick Suggestions</span>
                    <button 
                      type="button" 
                      onClick={() => setShowSuggestions(false)}
                      className="btn-close btn-sm p-0" 
                      aria-label="Close suggestions"
                    ></button>
                  </div>
                  {filteredSuggestions.map((sugg, idx) => (
                    <div 
                      key={idx} 
                      className="p-2 px-3 border-bottom text-dark text-start d-flex align-items-center justify-content-between"
                      style={{ cursor: 'pointer', transition: 'background 0.15s' }}
                      onMouseDown={() => handleSuggestionSelect(sugg)}
                    >
                      <div className="d-flex align-items-center gap-2 text-truncate">
                        {sugg.type === 'Category' ? (
                          <FaLeaf size={12} className="text-success flex-shrink-0" />
                        ) : (
                          <FaSpa size={12} className="text-warning flex-shrink-0" />
                        )}
                        <span className="small text-truncate">{sugg.label}</span>
                      </div>
                      <span className="badge bg-light text-muted small">{sugg.type}</span>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* User Controls & Cart & Portals */}
            <div className="d-flex align-items-center gap-2 gap-sm-3 flex-shrink-0">
              
              {/* Doctor Quick Consultation CTA on desktop */}
              <Link 
                to="/doctorconsult" 
                className="btn btn-outline-success btn-sm rounded-pill px-3 py-1 d-none d-lg-flex align-items-center gap-2 fw-semibold"
                style={{ fontSize: '0.86rem' }}
              >
                <FaCalendarCheck size={12} />
                <span>Book Vaidya</span>
              </Link>

              {/* Authentication Status */}
              {user ? (
                <div className="dropdown">
                  <button 
                    className="btn btn-outline-success btn-sm rounded-pill px-3 py-1 dropdown-toggle d-flex align-items-center gap-2"
                    type="button" 
                    id="userDropdown" 
                    data-bs-toggle="dropdown" 
                    aria-expanded="false"
                  >
                    <FaUser size={12} />
                    <span className="fw-semibold small">{user.username || 'Account'}</span>
                  </button>
                  <ul className="dropdown-menu dropdown-menu-end shadow border-0 rounded-4 mt-2 p-2" aria-labelledby="userDropdown">
                    <li className="px-3 py-2 border-bottom mb-1 bg-light rounded-3">
                      <small className="text-muted d-block" style={{ fontSize: '0.72rem' }}>Signed in as</small>
                      <strong className="text-dark small d-block text-truncate">{user.email || user.username}</strong>
                    </li>
                    <li>
                      <Link className="dropdown-item py-2 rounded-2 small" to="/profile">
                        <FaUser className="me-2 text-success" /> My Profile
                      </Link>
                    </li>
                    <li>
                      <Link className="dropdown-item py-2 rounded-2 small" to="/order-details">
                        <FaBoxOpen className="me-2 text-success" /> My Orders
                      </Link>
                    </li>
                    <li>
                      <Link className="dropdown-item py-2 rounded-2 small" to="/doctorconsult">
                        <FaUserMd className="me-2 text-success" /> Consultations
                      </Link>
                    </li>
                    <li><hr className="dropdown-divider my-1" /></li>
                    <li>
                      <button className="dropdown-item py-2 rounded-2 text-danger small" onClick={handleLogOut}>
                        <FaSignOutAlt className="me-2" /> Logout
                      </button>
                    </li>
                  </ul>
                </div>
              ) : doctor ? (
                <div className="d-flex align-items-center gap-2">
                  <Link to="/doctor-dashboard" className="btn btn-sm btn-outline-success rounded-pill px-3 fw-semibold">
                    <FaUserMd className="me-1" /> Dr. Panel
                  </Link>
                  <button onClick={handleLogOut} className="btn btn-sm btn-outline-danger rounded-pill px-2" title="Logout">
                    <FaSignOutAlt />
                  </button>
                </div>
              ) : admin ? (
                <div className="d-flex align-items-center gap-2">
                  <Link to="/admin-dashboard" className="btn btn-sm btn-outline-success rounded-pill px-3 fw-semibold">
                    <FaShieldAlt className="me-1" /> Admin
                  </Link>
                  <button onClick={handleLogOut} className="btn btn-sm btn-outline-danger rounded-pill px-2" title="Logout">
                    <FaSignOutAlt />
                  </button>
                </div>
              ) : (
                <div className="d-flex align-items-center gap-2">
                  <Link to="/user-account" className="btn-ayur-outline btn-sm py-1 px-3">
                    <FaUser size={12} /> <span className="d-none d-sm-inline">Sign In</span>
                  </Link>
                </div>
              )}

              {/* Shopping Cart Button */}
              <button 
                onClick={handleCartClick}
                className="btn position-relative rounded-pill py-1 px-3 d-flex align-items-center gap-2"
                style={{ 
                  background: '#eaf4ee', 
                  color: '#1b4332', 
                  border: '1px solid #d0e4d7', 
                  fontWeight: 600,
                  fontSize: '0.88rem'
                }}
                title="View Shopping Cart"
                aria-label="View Shopping Cart"
              >
                <FaShoppingCart size={15} />
                <span className="d-none d-sm-inline">Cart</span>
                {cartCount > 0 && (
                  <span className="badge rounded-pill bg-success ms-1" style={{ fontSize: '0.72rem' }}>
                    {cartCount}
                  </span>
                )}
              </button>

              {/* Mobile Drawer Toggle Button */}
              <button 
                className="btn btn-outline-success d-md-none p-2 rounded-3"
                onClick={() => setMobileDrawerOpen(true)}
                aria-label="Open Mobile Menu"
              >
                <FaBars size={18} />
              </button>
            </div>

          </div>

          {/* Mobile Search Input */}
          <div className="d-md-none mt-2 position-relative">
            <form onSubmit={handleSearchSubmit} className="d-flex align-items-center position-relative">
              <input
                type="search"
                className="ayur-search-input pe-5"
                placeholder="Search remedies, herbs, doshas..."
                value={searchValue}
                onChange={(e) => setSearchValue(e.target.value)}
              />
              <button 
                type="submit" 
                className="ayur-search-btn position-absolute" 
                style={{ right: '4px', top: '4px', bottom: '4px' }}
                aria-label="Search"
              >
                <FaSearch size={13} />
              </button>
            </form>
          </div>

        </div>
      </header>

      {/* 3. Main Navigation Bar (Desktop) */}
      <nav className="ayur-navbar d-none d-md-block">
        <div className="container-fluid px-lg-5">
          <ul className="navbar-nav d-flex flex-row justify-content-center align-items-center w-100 flex-wrap py-0 mb-0">
            <li className="nav-item">
              <Link to="/" className="ayur-nav-link">Home</Link>
            </li>
            <li className="nav-item">
              <Link to="/get-all-products" className="ayur-nav-link">Ayurvedic Medicines & Products</Link>
            </li>
            <li className="nav-item">
              <Link to="/yoga" className="ayur-nav-link">Yoga & Asanas</Link>
            </li>
            <li className="nav-item">
              <Link to="/diseases" className="ayur-nav-link">Diseases & Home Remedies</Link>
            </li>
            <li className="nav-item">
              <Link to="/doctorconsult" className="ayur-nav-link">Doctor Consultation</Link>
            </li>
            <li className="nav-item">
              <Link to="/about" className="ayur-nav-link">About Ayurveda</Link>
            </li>
            <li className="nav-item">
              <Link to="/contact" className="ayur-nav-link">Contact</Link>
            </li>
            
            {/* Quick access to portals */}
            {!doctor && (
              <li className="nav-item ms-lg-3">
                <Link to="/doctor-signIn" className="ayur-nav-link portal-badge">
                  <FaUserMd size={13} /> Doctor Portal
                </Link>
              </li>
            )}
            {!admin && (
              <li className="nav-item">
                <Link to="/admin-signIn" className="ayur-nav-link portal-badge">
                  <FaShieldAlt size={13} /> Admin Portal
                </Link>
              </li>
            )}
          </ul>
        </div>
      </nav>

      {/* 4. Working Responsive Mobile Drawer */}
      {mobileDrawerOpen && (
        <>
          <div 
            className="ayur-drawer-backdrop" 
            onClick={() => setMobileDrawerOpen(false)}
          />
          <div className="ayur-mobile-drawer">
            {/* Drawer Header */}
            <div className="p-3 border-bottom d-flex align-items-center justify-content-between bg-light">
              <div className="d-flex align-items-center gap-2">
                <img 
                  src={brandLogo} 
                  alt="Logo" 
                  style={{ width: '32px', height: '32px', borderRadius: '50%' }}
                />
                <span className="fw-bold font-cinzel small text-success">THE GREAT AYURVEDA</span>
              </div>
              <button 
                className="btn btn-sm btn-outline-secondary rounded-circle"
                onClick={() => setMobileDrawerOpen(false)}
                aria-label="Close Menu"
              >
                <FaTimes size={14} />
              </button>
            </div>

            {/* Drawer Links */}
            <div className="p-3 flex-grow-1 overflow-auto">
              <ul className="list-unstyled mb-4">
                <li className="mb-2">
                  <Link 
                    to="/" 
                    className="d-block py-2 px-3 rounded-3 text-dark text-decoration-none fw-semibold hover-bg-light"
                    onClick={() => setMobileDrawerOpen(false)}
                  >
                    Home
                  </Link>
                </li>
                <li className="mb-2">
                  <Link 
                    to="/get-all-products" 
                    className="d-block py-2 px-3 rounded-3 text-dark text-decoration-none fw-semibold"
                    onClick={() => setMobileDrawerOpen(false)}
                  >
                    Ayurvedic Medicines & Products
                  </Link>
                </li>
                <li className="mb-2">
                  <Link 
                    to="/yoga" 
                    className="d-block py-2 px-3 rounded-3 text-dark text-decoration-none fw-semibold"
                    onClick={() => setMobileDrawerOpen(false)}
                  >
                    Yoga & Asanas
                  </Link>
                </li>
                <li className="mb-2">
                  <Link 
                    to="/diseases" 
                    className="d-block py-2 px-3 rounded-3 text-dark text-decoration-none fw-semibold"
                    onClick={() => setMobileDrawerOpen(false)}
                  >
                    Diseases & Home Remedies
                  </Link>
                </li>
                <li className="mb-2">
                  <Link 
                    to="/doctorconsult" 
                    className="d-block py-2 px-3 rounded-3 text-dark text-decoration-none fw-semibold"
                    onClick={() => setMobileDrawerOpen(false)}
                  >
                    Doctor Consultation
                  </Link>
                </li>
                <li className="mb-2">
                  <Link 
                    to="/about" 
                    className="d-block py-2 px-3 rounded-3 text-dark text-decoration-none fw-semibold"
                    onClick={() => setMobileDrawerOpen(false)}
                  >
                    About Ayurveda
                  </Link>
                </li>
                <li className="mb-2">
                  <Link 
                    to="/contact" 
                    className="d-block py-2 px-3 rounded-3 text-dark text-decoration-none fw-semibold"
                    onClick={() => setMobileDrawerOpen(false)}
                  >
                    Contact Sanctuary
                  </Link>
                </li>
              </ul>

              <div className="border-top pt-3">
                <small className="text-muted fw-bold d-block text-uppercase mb-2" style={{ fontSize: '0.72rem' }}>
                  Professional Portals
                </small>
                <Link 
                  to="/doctor-signIn" 
                  className="d-flex align-items-center gap-2 py-2 px-3 rounded-3 text-dark text-decoration-none small mb-1"
                  onClick={() => setMobileDrawerOpen(false)}
                >
                  <FaUserMd className="text-success" /> Doctor Portal
                </Link>
                <Link 
                  to="/admin-signIn" 
                  className="d-flex align-items-center gap-2 py-2 px-3 rounded-3 text-dark text-decoration-none small"
                  onClick={() => setMobileDrawerOpen(false)}
                >
                  <FaShieldAlt className="text-warning" /> Administrator Portal
                </Link>
              </div>
            </div>

            {/* Drawer Footer User Status */}
            <div className="p-3 border-top bg-light">
              {user ? (
                <div className="d-flex justify-content-between align-items-center">
                  <div className="text-truncate me-2">
                    <small className="text-muted d-block">Logged in as</small>
                    <span className="fw-bold small text-dark text-truncate">{user.username}</span>
                  </div>
                  <button onClick={handleLogOut} className="btn btn-sm btn-outline-danger rounded-pill px-3">
                    Logout
                  </button>
                </div>
              ) : (
                <Link 
                  to="/user-account" 
                  className="btn-ayur-primary w-100 py-2 small"
                  onClick={() => setMobileDrawerOpen(false)}
                >
                  <FaUser size={12} /> Sign In to Account
                </Link>
              )}
            </div>
          </div>
        </>
      )}
    </>
  );
}

export default Header;
