import { createAsyncThunk, createSlice } from "@reduxjs/toolkit";
import axios from "axios";
import Api from "../components/WebApi/Api";

export const getPendingDoctor = createAsyncThunk("PendingDoctor/getPendingDoctor", async ()=>{
    try {
        let response=await axios.get(Api.getPendingDoctor);
        console.log(response.data.pendingDoctorInfo);
        return response.data.pendingDoctorInfo;
    } catch (error) {
        console.log(error)
    }
})

const slice=createSlice({
    name:'PendingDoctor',
    initialState:{
        pendingDoctorList:[],
        isLoading: false,
        error: null,
        current:0
    },
    reducers:{
        updateCurrent:(state,action)=>{
            console.log(action.payload)
            state.current=action.payload;
        }
    },
    extraReducers:(builder)=>{
        builder.addCase(getPendingDoctor.pending,(state,action)=>{
            state.isLoading=true;
        }).addCase(getPendingDoctor.fulfilled,(state,action)=>{
            state.isLoading=false;
            state.pendingDoctorList=action.payload;
        }).addCase(getPendingDoctor.rejected,(state,action)=>{
            state.isLoading = false;
            state.error = "Oops! something went wrong..";
        })
    }
})
export const {updateCurrent} = slice.actions;
export default slice.reducer;