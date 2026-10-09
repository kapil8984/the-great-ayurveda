import React from 'react';
import { BsGrid1X2Fill, BsPersonFill, BsCalendarCheck, BsHouseFill, BsShieldLockFill } from 'react-icons/bs';
import { useNavigate } from 'react-router-dom';
import '../style/doctor.css';

function Sidebar({ openSidebarToggle, OpenSidebar }) {
    const navigate = useNavigate();

    return (
        <aside id="sidebar" className={openSidebarToggle ? "sidebar-responsive" : ""}>
            <div className='sidebar-title'>
                <div className='sidebar-brand d-flex align-items-center gap-2'>
                    <span className="fs-4 text-success font-cinzel fw-bold">Ayurveda</span>
                </div>
                <span className='icon close_icon' onClick={OpenSidebar} style={{ cursor: 'pointer' }}>✕</span>
            </div>

            <ul className='sidebar-list'>
                <li className='sidebar-list-item'>
                    <span onClick={() => navigate('/doctor-dashboard')} style={{ cursor: 'pointer' }}>
                        <BsGrid1X2Fill className='icon' /> Appointments
                    </span>
                </li>
                <li className='sidebar-list-item'>
                    <span onClick={() => navigate('/doctor-dashboard/doctor-profile')} style={{ cursor: 'pointer' }}>
                        <BsPersonFill className='icon' /> Doctor Profile
                    </span>
                </li>
                <li className='sidebar-list-item'>
                    <span onClick={() => navigate('/doctor-dashboard/doctor-profile/add-details-doctor')} style={{ cursor: 'pointer' }}>
                        <BsCalendarCheck className='icon' /> Professional Details
                    </span>
                </li>
                <li className='sidebar-list-item'>
                    <span onClick={() => navigate('/doctor-dashboard/doctor-profile/change-password')} style={{ cursor: 'pointer' }}>
                        <BsShieldLockFill className='icon' /> Security / Password
                    </span>
                </li>
                <li className='sidebar-list-item'>
                    <span onClick={() => navigate('/')} style={{ cursor: 'pointer' }}>
                        <BsHouseFill className='icon' /> Return to Website
                    </span>
                </li>
            </ul>
        </aside>
    );
}

export default Sidebar;
