import React, { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import WebApi from '../WebApi/Api.js';
import axios from "axios";
import { toast } from "react-toastify";
import starGenerate from "../product/GenerateRating.js";
import { FaSearch, FaSpa, FaPlayCircle } from "react-icons/fa";

function ShowYoga() {
    const [yogaList, setYogaList] = useState([]);
    const [searchTerm, setSearchTerm] = useState("");
    const [loading, setLoading] = useState(true);
    const navigate = useNavigate();

    useEffect(() => {
        loadYogas();
    }, []);

    const loadYogas = async () => {
        try {
            setLoading(true);
            const response = await axios.get(WebApi.getAllYoga);
            const list = response.data.Yogas || response.data.yogas || [];
            setYogaList(list);
        } catch (err) {
            console.error(err);
            toast.error("Could not load yoga postures");
        } finally {
            setLoading(false);
        }
    };

    const handleYogaDetails = (yoga) => {
        navigate("/ShowYogaDetails", { state: { yoga } });
    };

    const filteredYogas = yogaList.filter((y) => {
        const name = (y.yogaName || "").toLowerCase();
        const desc = (y.Description || y.description || "").toLowerCase();
        const q = searchTerm.toLowerCase();
        return name.includes(q) || desc.includes(q);
    });

    return (
        <div className="container-fluid py-5 px-3 px-md-5">
            {/* Header Banner */}
            <div className="text-center mb-5">
                <span className="badge bg-success-subtle text-success px-3 py-2 rounded-pill fw-semibold mb-2">
                    Ayurvedic Yoga Chikitsa
                </span>
                <h1 className="display-6 fw-bold font-playfair text-dark mb-2">Therapeutic Yoga & Pranayama</h1>
                <p className="text-muted mx-auto" style={{ maxWidth: '650px' }}>
                    Ancient postures (Asanas) and breath-control practices designed to align subtle Prana channels, calm the mind, and heal physiological imbalances.
                </p>

                {/* Search Bar */}
                <div className="mx-auto mt-4" style={{ maxWidth: '480px' }}>
                    <div className="input-group shadow-sm rounded-pill overflow-hidden border">
                        <span className="input-group-text bg-white border-0 ps-4 text-muted">
                            <FaSearch />
                        </span>
                        <input
                            type="text"
                            className="form-control border-0 py-2"
                            placeholder="Search yoga asana or therapeutic benefit..."
                            value={searchTerm}
                            onChange={(e) => setSearchTerm(e.target.value)}
                        />
                    </div>
                </div>
            </div>

            {loading ? (
                <div className="py-5 text-center">
                    <div className="spinner-border text-success" role="status"></div>
                </div>
            ) : filteredYogas.length === 0 ? (
                <div className="text-center py-5 card border-0 shadow-sm rounded-4 p-5 mx-auto" style={{ maxWidth: '500px' }}>
                    <FaSpa size={48} className="text-muted mx-auto mb-3" />
                    <h5 className="fw-semibold text-secondary">No yoga practices found</h5>
                    <p className="text-muted small">Try a different search term to explore available postures.</p>
                </div>
            ) : (
                <div className="row g-4">
                    {filteredYogas.map((yoga, index) => {
                        const img = yoga.imageUrl || 'https://i.ibb.co/kSn83hn/hinduist-yoga-position.png';
                        const desc = yoga.Description || yoga.description || "";

                        return (
                            <div className="col-12 col-sm-6 col-lg-4" key={index}>
                                <div className="card h-100 border-0 shadow-sm rounded-4 overflow-hidden card-ayur d-flex flex-column">
                                    <div
                                        className="position-relative bg-white cursor-pointer"
                                        onClick={() => handleYogaDetails(yoga)}
                                        style={{ cursor: 'pointer' }}
                                    >
                                        <img
                                            src={img}
                                            alt={yoga.yogaName}
                                            className="card-img-top object-fit-cover"
                                            style={{ height: '220px' }}
                                            onError={(e) => { e.target.onerror = null; e.target.src = 'https://i.ibb.co/kSn83hn/hinduist-yoga-position.png'; }}
                                        />
                                        <div className="position-absolute top-0 end-0 m-3">
                                            <span className="badge bg-dark bg-opacity-75 rounded-pill px-3 py-1 small">
                                                Ayurvedic Asana
                                            </span>
                                        </div>
                                    </div>

                                    <div className="card-body p-4 d-flex flex-column flex-grow-1">
                                        <h5
                                            className="fw-bold font-playfair text-dark mb-1 cursor-pointer"
                                            onClick={() => handleYogaDetails(yoga)}
                                            style={{ cursor: 'pointer' }}
                                        >
                                            {yoga.yogaName}
                                        </h5>

                                        <p className="text-muted small mb-3 text-truncate-2" style={{ height: '40px', overflow: 'hidden' }}>
                                            {desc}
                                        </p>

                                        <div className="mb-3">
                                            {starGenerate(yoga.averageRating || 5)}
                                        </div>

                                        <button
                                            onClick={() => handleYogaDetails(yoga)}
                                            className="btn btn-outline-success rounded-pill w-100 mt-auto fw-semibold d-flex align-items-center justify-content-center gap-2"
                                        >
                                            <FaPlayCircle /> Start Guided Practice
                                        </button>
                                    </div>
                                </div>
                            </div>
                        );
                    })}
                </div>
            )}
        </div>
    );
}

export default ShowYoga;
