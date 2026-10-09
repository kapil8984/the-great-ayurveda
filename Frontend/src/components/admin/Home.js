import React, { useEffect, useState } from "react";
import { Outlet, useNavigate, useLocation } from "react-router-dom";
import Api, { apiClient } from "../WebApi/Api";
import { 
    FaUsers, FaUserMd, FaClock, FaBoxOpen, 
    FaExclamationTriangle, FaShoppingBag, FaRupeeSign, 
    FaPlusCircle, FaSpa, FaCheckCircle, FaArrowRight 
} from "react-icons/fa";

function Home() {
    const navigate = useNavigate();
    const location = useLocation();
    const [stats, setStats] = useState(null);
    const [loading, setLoading] = useState(true);

    const isChildRoute = location.pathname !== "/admin-dashboard" && location.pathname !== "/admin-dashboard/";

    useEffect(() => {
        if (!isChildRoute) {
            loadDashboardStats();
        }
    }, [location.pathname]);

    const loadDashboardStats = async () => {
        try {
            setLoading(true);
            const response = await apiClient.get(Api.getAdminDashboard);
            setStats(response.data);
        } catch (error) {
            console.error("Dashboard error:", error);
        } finally {
            setLoading(false);
        }
    };

    if (isChildRoute) {
        return (
            <div className="p-4 w-100">
                <Outlet />
            </div>
        );
    }

    return (
        <div className="p-4 p-md-5 w-100">
            {/* Header */}
            <div className="d-flex flex-wrap justify-content-between align-items-center mb-4 gap-3">
                <div>
                    <span className="badge bg-success-subtle text-success px-3 py-2 rounded-pill fw-semibold mb-2">
                        Control Center
                    </span>
                    <h2 className="fw-bold font-playfair text-dark mb-1">Administrative Overview</h2>
                    <p className="text-muted small mb-0">System performance, practitioner approvals, and pharmacy operations</p>
                </div>

                <div className="d-flex gap-2">
                    <button
                        onClick={() => navigate('/admin-dashboard/add-product')}
                        className="btn btn-primary-ayur text-white rounded-pill px-3 btn-sm d-flex align-items-center gap-2 fw-semibold"
                    >
                        <FaPlusCircle /> Add Product
                    </button>
                    <button
                        onClick={() => navigate('/admin-dashboard/add-yoga')}
                        className="btn btn-outline-success rounded-pill px-3 btn-sm d-flex align-items-center gap-2 fw-semibold"
                    >
                        <FaSpa /> Add Yoga
                    </button>
                </div>
            </div>

            {loading ? (
                <div className="py-5 text-center">
                    <div className="spinner-border text-success" role="status"></div>
                </div>
            ) : (
                <>
                    {/* Stats Metrics Cards */}
                    <div className="row g-4 mb-5">
                        <div className="col-12 col-sm-6 col-xl-3">
                            <div className="card border-0 shadow-sm rounded-4 p-4 card-ayur bg-white">
                                <div className="d-flex align-items-center justify-content-between mb-3">
                                    <span className="text-muted small fw-semibold">Total Patients</span>
                                    <div className="rounded-circle bg-primary-subtle text-primary p-3">
                                        <FaUsers size={22} />
                                    </div>
                                </div>
                                <h3 className="fw-bold text-dark mb-0 font-playfair">{stats?.totalUsers || 0}</h3>
                                <small className="text-muted">Registered accounts</small>
                            </div>
                        </div>

                        <div className="col-12 col-sm-6 col-xl-3">
                            <div className="card border-0 shadow-sm rounded-4 p-4 card-ayur bg-white">
                                <div className="d-flex align-items-center justify-content-between mb-3">
                                    <span className="text-muted small fw-semibold">Active Doctors</span>
                                    <div className="rounded-circle bg-success-subtle text-success p-3">
                                        <FaUserMd size={22} />
                                    </div>
                                </div>
                                <h3 className="fw-bold text-dark mb-0 font-playfair">{stats?.approvedDoctors || 0}</h3>
                                <small className="text-muted">Approved Vaidyas</small>
                            </div>
                        </div>

                        <div className="col-12 col-sm-6 col-xl-3">
                            <div
                                className="card border-0 shadow-sm rounded-4 p-4 card-ayur bg-white cursor-pointer"
                                onClick={() => navigate('/admin-dashboard/pending-doctor-request')}
                                style={{ cursor: 'pointer' }}
                            >
                                <div className="d-flex align-items-center justify-content-between mb-3">
                                    <span className="text-muted small fw-semibold">Pending Approvals</span>
                                    <div className="rounded-circle bg-warning-subtle text-warning p-3">
                                        <FaClock size={22} />
                                    </div>
                                </div>
                                <h3 className="fw-bold text-warning mb-0 font-playfair">{stats?.pendingDoctors || 0}</h3>
                                <small className="text-warning fw-semibold d-flex align-items-center gap-1">
                                    Needs review <FaArrowRight size={10} />
                                </small>
                            </div>
                        </div>

                        <div className="col-12 col-sm-6 col-xl-3">
                            <div className="card border-0 shadow-sm rounded-4 p-4 card-ayur bg-white">
                                <div className="d-flex align-items-center justify-content-between mb-3">
                                    <span className="text-muted small fw-semibold">Total Revenue</span>
                                    <div className="rounded-circle bg-success-subtle text-success p-3">
                                        <FaRupeeSign size={22} />
                                    </div>
                                </div>
                                <h3 className="fw-bold text-success mb-0 font-playfair">₹ {Number(stats?.totalRevenue || 0).toFixed(2)}</h3>
                                <small className="text-muted">{stats?.totalOrders || 0} orders processed</small>
                            </div>
                        </div>

                        <div className="col-12 col-sm-6 col-xl-3">
                            <div className="card border-0 shadow-sm rounded-4 p-4 card-ayur bg-white">
                                <div className="d-flex align-items-center justify-content-between mb-3">
                                    <span className="text-muted small fw-semibold">Total Products</span>
                                    <div className="rounded-circle bg-info-subtle text-info p-3">
                                        <FaBoxOpen size={22} />
                                    </div>
                                </div>
                                <h3 className="fw-bold text-dark mb-0 font-playfair">{stats?.totalProducts || 0}</h3>
                                <small className="text-muted">In Ayurvedic catalog</small>
                            </div>
                        </div>

                        <div className="col-12 col-sm-6 col-xl-3">
                            <div className="card border-0 shadow-sm rounded-4 p-4 card-ayur bg-white">
                                <div className="d-flex align-items-center justify-content-between mb-3">
                                    <span className="text-muted small fw-semibold">Low Stock Alert</span>
                                    <div className="rounded-circle bg-danger-subtle text-danger p-3">
                                        <FaExclamationTriangle size={22} />
                                    </div>
                                </div>
                                <h3 className="fw-bold text-danger mb-0 font-playfair">{stats?.lowStockProducts || 0}</h3>
                                <small className="text-muted">≤ 5 units left</small>
                            </div>
                        </div>

                        <div className="col-12 col-sm-6 col-xl-3">
                            <div className="card border-0 shadow-sm rounded-4 p-4 card-ayur bg-white">
                                <div className="d-flex align-items-center justify-content-between mb-3">
                                    <span className="text-muted small fw-semibold">Completed Orders</span>
                                    <div className="rounded-circle bg-primary-subtle text-primary p-3">
                                        <FaShoppingBag size={22} />
                                    </div>
                                </div>
                                <h3 className="fw-bold text-dark mb-0 font-playfair">{stats?.totalOrders || 0}</h3>
                                <small className="text-muted">Customer transactions</small>
                            </div>
                        </div>

                        <div className="col-12 col-sm-6 col-xl-3">
                            <div className="card border-0 shadow-sm rounded-4 p-4 card-ayur bg-white">
                                <div className="d-flex align-items-center justify-content-between mb-3">
                                    <span className="text-muted small fw-semibold">Active Categories</span>
                                    <div className="rounded-circle bg-secondary-subtle text-secondary p-3">
                                        <FaCheckCircle size={22} />
                                    </div>
                                </div>
                                <h3 className="fw-bold text-dark mb-0 font-playfair">{stats?.totalCategories || 6}</h3>
                                <small className="text-muted">Herbal taxonomy</small>
                            </div>
                        </div>
                    </div>

                    {/* Quick Access Tiles */}
                    <div className="row g-4 mb-4">
                        <div className="col-12 col-md-4">
                            <div
                                onClick={() => navigate('/admin-dashboard/pending-doctor-request')}
                                className="card border-0 shadow-sm rounded-4 p-4 text-center cursor-pointer card-ayur transition-all bg-white"
                                style={{ cursor: 'pointer' }}
                            >
                                <FaUserMd size={36} className="text-success mx-auto mb-3" />
                                <h5 className="fw-bold font-playfair text-dark">Practitioner Verification</h5>
                                <p className="text-muted small mb-0">Review BAMS certificates, clinic licenses, and approval requests.</p>
                            </div>
                        </div>

                        <div className="col-12 col-md-4">
                            <div
                                onClick={() => navigate('/admin-dashboard/add-product')}
                                className="card border-0 shadow-sm rounded-4 p-4 text-center cursor-pointer card-ayur transition-all bg-white"
                                style={{ cursor: 'pointer' }}
                            >
                                <FaBoxOpen size={36} className="text-success mx-auto mb-3" />
                                <h5 className="fw-bold font-playfair text-dark">Product Management</h5>
                                <p className="text-muted small mb-0">Add new herbs, adjust inventory levels, prices, and formulations.</p>
                            </div>
                        </div>

                        <div className="col-12 col-md-4">
                            <div
                                onClick={() => navigate('/admin-dashboard/add-yoga')}
                                className="card border-0 shadow-sm rounded-4 p-4 text-center cursor-pointer card-ayur transition-all bg-white"
                                style={{ cursor: 'pointer' }}
                            >
                                <FaSpa size={36} className="text-success mx-auto mb-3" />
                                <h5 className="fw-bold font-playfair text-dark">Yoga Directory</h5>
                                <p className="text-muted small mb-0">Publish guided yogic asanas, benefits, and instructional videos.</p>
                            </div>
                        </div>
                    </div>
                </>
            )}
        </div>
    );
}

export default Home;