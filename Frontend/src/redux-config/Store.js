import { configureStore } from "@reduxjs/toolkit";
import PendinDoctorSlice from "./PendinDoctorSlice";
import ProductSlice from "./ProductSlice";

const store=configureStore({
    reducer:{
        pendingDoctor:PendinDoctorSlice,
        getProduct:ProductSlice,
    }
})
export default store;