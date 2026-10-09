import React from 'react'
import { BsPersonCircle, BsSearch, BsJustify }
    from 'react-icons/bs'
import '../style/doctor.css'
import { useNavigate } from 'react-router-dom'
function Header({ OpenSidebar }) {
    const navigate=useNavigate();
    return (
        <header className='header position-relative'>
            <div className='menu-icon'>
                <BsJustify className='icon' onClick={OpenSidebar} />
            </div>
            <div className='header-left'>
                <BsSearch className='icon' />
            </div>
            <div className='header-right bg-light p-3' onClick={()=>navigate('doctor-profile')} style={{ cursor: 'pointer',zIndex:'10' }}>
                <BsPersonCircle className='icon' />Profile
            </div>
        </header>
    )
}

export default Header;