import React from 'react';
import { BsGrid1X2Fill, BsFillArchiveFill, BsFillGrid3X3GapFill, BsPersonCheckFill, BsHouseFill } from 'react-icons/bs';
import { useNavigate } from 'react-router-dom';

function Sidebar({ openSidebarToggle, OpenSidebar }) {
    const navigate = useNavigate();

    return (
        <aside id="sidebar" className={openSidebarToggle ? "sidebar-responsive" : ""}>
            <div className='sidebar-title'>
                <div className='sidebar-brand d-flex align-items-center gap-2'>
                    <span className="fs-4 text-warning font-cinzel fw-bold">Ayurveda Admin</span>
                </div>
                <span className='icon close_icon' onClick={OpenSidebar} style={{ cursor: 'pointer' }}>✕</span>
            </div>

            <ul className='sidebar-list'>
                <li className='sidebar-list-item'>
                    <span onClick={() => navigate('/admin-dashboard')} style={{ cursor: 'pointer' }}>
                        <BsGrid1X2Fill className='icon' /> Dashboard Overview
                    </span>
                </li>
                <li className='sidebar-list-item'>
                    <span onClick={() => navigate('/admin-dashboard/pending-doctor-request')} style={{ cursor: 'pointer' }}>
                        <BsPersonCheckFill className='icon' /> Doctor Approvals
                    </span>
                </li>
                <li className='sidebar-list-item'>
                    <span onClick={() => navigate('/admin-dashboard/add-product')} style={{ cursor: 'pointer' }}>
                        <BsFillArchiveFill className='icon' /> Add Medicine / Product
                    </span>
                </li>
                <li className='sidebar-list-item'>
                    <span onClick={() => navigate('/admin-dashboard/add-yoga')} style={{ cursor: 'pointer' }}>
                        <BsFillGrid3X3GapFill className='icon' /> Add Yoga Asana
                    </span>
                </li>
                <li className='sidebar-list-item'>
                    <span onClick={() => navigate('/')} style={{ cursor: 'pointer' }}>
                        <BsHouseFill className='icon' /> View Website
                    </span>
                </li>
            </ul>
        </aside>
    );
}

export default Sidebar;
