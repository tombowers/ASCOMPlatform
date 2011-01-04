﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Optec;
using System.Diagnostics;

namespace PyxisAPI
{
    public abstract class PyxisSettings : XmlSettingsManager
    {

        private enum XmlPropNames 
        {
            ComPortName, ParkPosition, HomeOnStart, LoggerTraceLevel, StepTime, SkyPAOffset
        }
        private int skyPAOffset = 0;
        private int stepTime = 8;
        private bool StepTimeIsDefault = true;
        private OptecPyxis.DeviceTypes deviceType;

        public PyxisSettings(OptecPyxis.DeviceTypes type) : base("Pyxis")
        {
            ReloadSettings(type);           
        }

        public void ReloadSettings(OptecPyxis.DeviceTypes type)
        {
            deviceType = type;
            skyPAOffset = int.Parse(GetPropertyFromXml(XmlPropNames.SkyPAOffset.ToString(), "0"));
            string stepTimeString = GetPropertyFromXml(XmlPropNames.StepTime.ToString(), "Default");
            if (stepTimeString == "Default")
            {
                StepTimeIsDefault = true;
            }
            else
            {
                StepTimeIsDefault = false;
                stepTime = int.Parse(stepTimeString);
            }
        }
  
        public string SavedSerialPortName
        {
            get { return GetPropertyFromXml(XmlPropNames.ComPortName.ToString(), "COM1"); }
            set { SetPropertyInXml(XmlPropNames.ComPortName.ToString(), value); }
        }

        public int ParkPosition
        {
            get { return int.Parse(GetPropertyFromXml(
                XmlPropNames.ParkPosition.ToString(), 
                "0"));}
            set { SetPropertyInXml(XmlPropNames.ParkPosition.ToString(), value.ToString()); }
        } 

        public TraceLevel LoggerTraceLevel
        {
            get
            {
                return (TraceLevel)Enum.Parse(typeof(TraceLevel),
                    GetGlobalSettingFromXml(XmlPropNames.LoggerTraceLevel.ToString(),
                    TraceLevel.Warning.ToString()));
            }
            set
            {
                SetGlobalSettingsInXml(XmlPropNames.LoggerTraceLevel.ToString(), value.ToString());
            }
        }

        public int StepTime
        {
            get 
            {
                if (StepTimeIsDefault)
                {
                    if (deviceType == OptecPyxis.DeviceTypes.TwoInch)
                        return 8;
                    else return 3;
                }
                else return stepTime; 
            }
            set 
            {
                StepTimeIsDefault = false;
                stepTime = value;
                SetPropertyInXml(XmlPropNames.StepTime.ToString(), value.ToString()); 
            }
        }

        /// <summary>
        /// Do Not set this property from anywhere other than the OptecPyxis class.
        /// </summary>
        public int SkyPAOffset
        {
            get { return skyPAOffset; }
            set 
            {
                skyPAOffset = value;
                SetPropertyInXml(XmlPropNames.SkyPAOffset.ToString(), value.ToString()); 
            }
        }

        public bool HomeOnStart
        {
            get 
            { 
                return bool.Parse(GetPropertyFromXml(XmlPropNames.HomeOnStart.ToString(),
                true.ToString())); 
            }
            set { SetPropertyInXml(XmlPropNames.HomeOnStart.ToString(), value.ToString()); }
        }

    }

    
}
