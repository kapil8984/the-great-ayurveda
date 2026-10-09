import axios from "axios";
import React, { useEffect, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import Api from "../WebApi/Api";
import { toast } from "react-toastify";

function ChangePassword() {
    const [doctor, setDoctor] = useState({});
    const oldPasswordInput = useRef();
    const newPasswordInput = useRef();
    const confirmPasswordInput = useRef();
    const location = useLocation();
    const navigate = useNavigate();
    useEffect(() => {
        setDoctor(location.state.doctor);
        !sessionStorage.getItem('doctor-details') && navigate('/');
    }, [location.state.doctor])
    const handleChangePassword = async () => {
        try {
            const oldPassword = oldPasswordInput.current.value;
            const newPassword = newPasswordInput.current.value;
            const doctorId = doctor._id;
            console.log(oldPassword)
            const response = await axios.post(Api.changePassword, { doctorId, newPassword, oldPassword })
            toast.success(response.data.message);
            navigate(-1)
        } catch (error) {
            toast.error(error.response.data.message)
        }
    };

    return (
        <div className="container-fluid d-flex justify-content-center align-items-center">
            <div className="border-2 border-success" style={{ width: '80%' }}>
                <label className="my-2">Old Password</label>
                <input type='password' ref={oldPasswordInput} className="form-control" />
                <label className="my-2">New Password</label>
                <input type='password' ref={newPasswordInput} className="form-control" />
                <label className="my-2">Confirm Password</label>
                <input type='password' ref={confirmPasswordInput} className="form-control mb-3" />
                <button className="btn btn-success my-2" onClick={handleChangePassword}>Change Password</button>
                <button className="btn btn-danger my-2 ms-3" onClick={() => navigate(-1)}>Cancel</button>
            </div>
        </div>
    );
}

export default ChangePassword;
