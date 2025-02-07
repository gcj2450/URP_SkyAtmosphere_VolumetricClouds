// Copyright (c) 2011-2012 Sundog Software LLC. All rights reserved worldwide.

using UnityEngine;
using System;
using System.Collections.Generic;

public class SilverLiningSky
{
    private double PI = 3.14159265;
    private float PIf = 3.14159265f;
    protected double DEGREES (double x)
    {
        return x * (180.0 / PI);
    }
    protected double RADIANS (double x)
    {
        return x * (PI / 180.0);
    }
    protected float RADIANS (float x)
    {
        return x * (PIf / 180.0f);
    }

    protected double NITS (double irradiance)
    {
        return irradiance * 683.0 / 3.14;
    }
	
	protected Vector3 XYZtoxyY(Vector3 XYZ)
	{
		Vector3 xyY;
	    xyY.x = XYZ.x / (XYZ.x + XYZ.y + XYZ.z);
	    xyY.y = XYZ.y / (XYZ.x + XYZ.y + XYZ.z);
	    xyY.z = XYZ.y;
		
		return xyY;
	}

    public float XBoost = 0, YBoost = 0, ZBoost = 0;
    public int boostExp = 3;
    public float sunTransmissionScale = 1.0f, sunScatteredScale = 1.0f;
    public float moonTransmissionScale = 1.0f, moonScatteredScale = 1.0f;
    public double aScale = 1.0, bScale = 1.0, cScale = 1.0, dScale = 1.0, eScale = 1.0;
    public double moonScale = 0.01;
    public double sunLuminanceScale = 1.0, moonLuminanceScale = 0.1;
    private double H = 8435.0;
    public float oneOverGamma = 0.45f;
    public float sunDistance = 90000.0f;
    public float sunWidthDegrees = 1.0f;
    public float moonDistance = 90000.0f;
    public float moonWidthDegrees = 1.0f;
    public float duskZenithLuminance = 0.02f;
    public float fogThickness = 500.0f;
    public int sphereSegments = 32;
	public float groundAlbedo = 0.3f;

    public SilverLiningSky ()
    {
        sunDistance *= (float)SilverLining.unitScale;
        moonDistance *= (float)SilverLining.unitScale;
        H *= (float)SilverLining.unitScale;

        ephemeris = new SilverLiningEphemeris ();
        InitTwilightLuminances ();
        sunSpectrum = new SilverLiningSolarSpectrum ();
        lunarSpectrum = new SilverLiningLunarSpectrum ();
        XYZ2RGB = new SilverLiningMatrix3 (3.240479, -0.969256, 0.055648, -1.537150, 1.875992, -0.204043, -0.498535, 0.041556, 1.057311);

        XYZ2RGB4 = new Matrix4x4 ();
        XYZ2RGB4[0, 0] = 3.240479f;
        XYZ2RGB4[0, 1] = -0.969256f;
        XYZ2RGB4[0, 2] = 0.055648f;
        XYZ2RGB4[0, 3] = 0.0f;
        XYZ2RGB4[1, 0] = -1.537150f;
        XYZ2RGB4[1, 1] = 1.875992f;
        XYZ2RGB4[1, 2] = -0.204043f;
        XYZ2RGB4[1, 3] = 0.0f;
        XYZ2RGB4[2, 0] = -0.498535f;
        XYZ2RGB4[2, 1] = 0.041556f;
        XYZ2RGB4[2, 2] = 1.057311f;
        XYZ2RGB4[2, 3] = 0.0f;
        XYZ2RGB4[3, 0] = 0.0f;
        XYZ2RGB4[3, 1] = 0.0f;
        XYZ2RGB4[3, 2] = 0.0f;
        XYZ2RGB4[3, 3] = 1.0f;
		
		hosekWilkieRadiances = new double[3];
		hosekWilkieCoeffs = new double[3,9];
		
		datasetsXYZ = new double[][]
		{
			datasetXYZ1,
			datasetXYZ2,
			datasetXYZ3
		};
		
		datasetsXYZRad = new double[][]
		{
			datasetXYZRad1,
			datasetXYZRad2,
			datasetXYZRad3
		};
    }

    public void Start ()
    {
        sun = GameObject.Find ("SilverLiningSun");
        Transform sunTrans = sun.GetComponent<Transform> ();
        sunTrans.position = new Vector3 (sunDistance, 0.0f, 0.0f);
        const float sunDiscPer = (256.0f - (57.0f * 2.0f)) / 256.0f;
        float sunDiscSize = sunWidthDegrees * (1.0f / sunDiscPer);
       

        moon = GameObject.Find ("SilverLiningMoon");
        Transform moonTrans = moon.GetComponent<Transform> ();
        moonTrans.position = new Vector3 (moonDistance, 0.0f, 0.0f);
        
        const float moonDiscPer = (256.0f - (3.0f * 2.0f)) / 256.0f;
        float moonDiscSize = moonWidthDegrees * (1.0f / moonDiscPer);
       
        starFogShader = Shader.Find("Particles/Stars");
        starNoFogShader = Shader.Find("Particles/Stars No Fog");

        moonTextures = new Texture[30];
        for (int i = 0; i < 30; i++) {
            String texName = "moonday" + (i + 1);
            moonTextures[i] = (Texture)Resources.Load (texName);
        }

        sunLight = GameObject.Find ("SilverLiningSunLight");
        moonLight = GameObject.Find ("SilverLiningMoonLight");
        
        stars = new SilverLiningStars (ephemeris);

        createSphere();
    }
	
	public Vector3 EquatorialToHorizon(double rightAscension, double declination)
	{
		double ra = rightAscension * Math.PI / 180.0;
		double dec = declination * Math.PI / 180.0;
        Vector3 starPos = new Vector3 ();
        starPos.x = (float)(Math.Cos (ra) * Math.Cos (dec));
        starPos.y = (float)(Math.Sin (ra) * Math.Cos (dec));
        starPos.z = (float)(Math.Sin (dec));	
		
		SilverLiningMatrix3 equatorialToHorizon = ephemeris.GetEquatorialToHorizonMatrix ();
		return equatorialToHorizon * starPos;
	}
	
    public void Update (SilverLiningTime time, SilverLiningLocation loc, Renderer renderer, bool bIsOvercast,
        bool doFog)
    {
        ephemeris.Update (time, loc);

        isOvercast = bIsOvercast;

        lightingChanged = false;
        ComputeSun (loc.GetAltitude ());
        ComputeMoon (loc.GetAltitude ());
        
        UpdatePerezCoefficients ();
		UpdateHosekWilkieCoefficients();
		
        UpdateZenith (loc.GetAltitude ());
        
        sunx = Perezx (0, thetaS);
        suny = Perezy (0, thetaS);
        sunY = PerezY (0, thetaS);
        moonY = PerezY (0, thetaM);
        moonx = Perezx (0, thetaM);
        moony = Perezy (0, thetaM);
        
        ComputeLogAvg ();
        ComputeToneMappedSkyLight ();
        
        renderer.material.SetColor ("theColor", Color.cyan);
        
        Vector4 sunPerez = new Vector4 ((float)sunx, (float)suny, (float)sunY, 1.0f);
        Vector4 zenithPerez = new Vector4 ((float)xZenith, (float)yZenith, (float)YZenith, 1.0f);
        Vector4 moonPerez = new Vector4 ((float)moonx, (float)moony, (float)moonY, 1.0f);
        Vector4 zenithMoonPerez = new Vector4 ((float)xMoon, (float)yMoon, (float)YMoon, 1.0f);
        
        Vector4 xPerezABC = new Vector4 ((float)Ax, (float)Bx, (float)Cx, 1.0f);
        Vector4 xPerezDE = new Vector4 ((float)Dx, (float)Ex, 0.0f, 1.0f);
        Vector4 yPerezABC = new Vector4 ((float)Ay, (float)By, (float)Cy, 1.0f);
        Vector4 yPerezDE = new Vector4 ((float)Dy, (float)Ey, 0, 0);
        Vector4 YPerezABC = new Vector4 ((float)AY, (float)BY, (float)CY, 1.0f);
        Vector4 YPerezDE = new Vector4 ((float)DY, (float)EY, 0, 0);
        
        double sfRod, sfCone;
        SilverLiningLuminanceMapper.GetLuminanceScales (out sfRod, out sfCone);
        Vector4 luminanceScales = new Vector4 ((float)sfRod, (float)sfCone, 0, 0);
        
        Vector4 kAndLdmax = new Vector4 ((float)SilverLiningLuminanceMapper.GetRodConeBlend (), (float)SilverLiningLuminanceMapper.GetMaxDisplayLuminance (), oneOverGamma, 1.0f);
        
        Vector3 sunPos = ephemeris.GetSunPositionHorizon ();
        sunPos.Normalize ();
        
        Vector3 moonPos = ephemeris.GetMoonPositionHorizon ();
        moonPos.Normalize ();

        Vector4 overcast = new Vector4 (isOvercast ? 1 : 0, isOvercast ? overcastBlend : 0, overcastTransmission, 0.0f);

        Color fogColor = new Color (1.0f, 1.0f, 1.0f, 1.0f);
        float fogDensity = 0;
        float fogDistance = 1E20f;
        
        if (RenderSettings.fog && doFog) {
            fogColor = RenderSettings.fogColor;
            fogDensity = RenderSettings.fogDensity;
            fogDistance = fogThickness;
        }

        Vector4 fog = new Vector4 (fogColor.r, fogColor.g, fogColor.b, fogDensity);
        overcast.w = fogDistance;

        renderer.material.SetVector ("sunPos", sunPos);
        renderer.material.SetVector ("moonPos", moonPos);
        renderer.material.SetVector ("sunPerez", sunPerez);
        renderer.material.SetVector ("moonPerez", moonPerez);
        renderer.material.SetVector ("zenithMoonPerez", zenithMoonPerez);
        renderer.material.SetVector ("zenithPerez", zenithPerez);

        renderer.material.SetVector ("xPerezABC", xPerezABC);
        renderer.material.SetVector ("xPerezDE", xPerezDE);
        renderer.material.SetVector ("yPerezABC", yPerezABC);
        renderer.material.SetVector ("yPerezDE", yPerezDE);
        renderer.material.SetVector ("YPerezABC", YPerezABC);
        renderer.material.SetVector ("YPerezDE", YPerezDE);
        renderer.material.SetVector ("luminanceScales", luminanceScales);
        renderer.material.SetVector ("kAndLdmax", kAndLdmax);
        renderer.material.SetVector ("overcast", overcast);
        renderer.material.SetVector ("fog", fog);
        renderer.material.SetMatrix ("XYZtoRGB", XYZ2RGB4);
		
        Vector4 XHosekABC = new Vector4((float)hosekWilkieCoeffs[0,0], (float)hosekWilkieCoeffs[0,1], (float)hosekWilkieCoeffs[0,2], 1.0f);
        Vector4 XHosekDEF = new Vector4((float)hosekWilkieCoeffs[0,3], (float)hosekWilkieCoeffs[0,4], (float)hosekWilkieCoeffs[0,5], 1.0f);
        Vector4 XHosekGHI = new Vector4((float)hosekWilkieCoeffs[0,6], (float)hosekWilkieCoeffs[0,7], (float)hosekWilkieCoeffs[0,8], 1.0f);

        Vector4 YHosekABC = new Vector4((float)hosekWilkieCoeffs[1,0], (float)hosekWilkieCoeffs[1,1], (float)hosekWilkieCoeffs[1,2], 1.0f);
        Vector4 YHosekDEF = new Vector4((float)hosekWilkieCoeffs[1,3], (float)hosekWilkieCoeffs[1,4], (float)hosekWilkieCoeffs[1,5], 1.0f);
        Vector4 YHosekGHI = new Vector4((float)hosekWilkieCoeffs[1,6], (float)hosekWilkieCoeffs[1,7], (float)hosekWilkieCoeffs[1,8], 1.0f);

        Vector4 ZHosekABC = new Vector4((float)hosekWilkieCoeffs[2,0], (float)hosekWilkieCoeffs[2,1], (float)hosekWilkieCoeffs[2,2], 1.0f);
        Vector4 ZHosekDEF = new Vector4((float)hosekWilkieCoeffs[2,3], (float)hosekWilkieCoeffs[2,4], (float)hosekWilkieCoeffs[2,5], 1.0f);
        Vector4 ZHosekGHI = new Vector4((float)hosekWilkieCoeffs[2,6], (float)hosekWilkieCoeffs[2,7], (float)hosekWilkieCoeffs[2,8], 1.0f);

        Vector4 HosekRadiances = new Vector4((float)hosekWilkieRadiances[0], (float)hosekWilkieRadiances[1], (float)hosekWilkieRadiances[2], perezBlend);
		
		renderer.material.SetVector ("XHosekABC", XHosekABC);
		renderer.material.SetVector ("XHosekDEF", XHosekDEF);
		renderer.material.SetVector ("XHosekGHI", XHosekGHI);
		
		renderer.material.SetVector ("YHosekABC", YHosekABC);
		renderer.material.SetVector ("YHosekDEF", YHosekDEF);
		renderer.material.SetVector ("YHosekGHI", YHosekGHI);
		
		renderer.material.SetVector ("ZHosekABC", ZHosekABC);
		renderer.material.SetVector ("ZHosekDEF", ZHosekDEF);
		renderer.material.SetVector ("ZHosekGHI", ZHosekGHI);
		
		renderer.material.SetVector ("HosekRadiances", HosekRadiances);
		
        UpdateSun ();
        UpdateMoon ();
        UpdateLight ();
        if (YZenith < duskZenithLuminance) {
            stars.Enable (true);
            stars.Update ();
        } else {
            stars.Enable (false);
        }

        if (starFogShader != null && starNoFogShader != null) {
            stars.SetShader(doFog ? starFogShader : starNoFogShader);
        }
    }

    private void createSphere()
    {
        int i, j, ntri, nvec;

        nvec = 2 + sphereSegments * sphereSegments * 2;
        ntri = (sphereSegments * 4 + sphereSegments * 4 * (sphereSegments - 1));

        GameObject sky = GameObject.Find("_SilverLiningSky");
        if (sky == null) {
            Debug.LogError("_SilverLiningSky not found!");
            return;
        }
        MeshFilter meshFilter = sky.GetComponent<MeshFilter>();
        if (meshFilter==null){
            Debug.LogError("MeshFilter not found!");
            return;
        }
        Mesh mesh = meshFilter.sharedMesh;
        if (mesh == null){
            meshFilter.mesh = new Mesh();
            mesh = meshFilter.sharedMesh;
        }
        mesh.Clear();

        Vector3[] verts = new Vector3[nvec];
        int[] triangles = new int[ntri * 3];

        float dj = (float)Math.PI / ((float)sphereSegments + 1.0f);
        float di = (float)Math.PI / (float)sphereSegments;

        verts[0] = new Vector3(0, 1, 0);
        verts[1] = new Vector3(0, -1, 0);

        for (j=0; j < sphereSegments; j++) {
            for (i=0; i < sphereSegments * 2; i++) {
                float y = (float)Math.Cos((j + 1) * dj);
                float x = (float)Math.Sin(i * di) * (float)Math.Sin((j + 1) * dj);
                float z = (float)Math.Cos(i * di) * (float)Math.Sin((j + 1) * dj);
                verts[2+i+j*sphereSegments*2] = new Vector3(x, y, z);
            }
        }

        for (i=0; i < sphereSegments * 2; i++) {
            triangles[3*i] = 0;
            triangles[3*i+1] = i + 2;
            triangles[3*i+2] = i + 3;
            if (i == sphereSegments * 2 - 1) {
                triangles[3*i+2] = 2;
            }
        }

        int v;
        int ind;
        for (j = 0; j < sphereSegments - 1; j++) {
            v = 2+j*sphereSegments*2;
            ind = 3*sphereSegments*2 + j*6*sphereSegments*2;
            for (i = 0; i < sphereSegments * 2; i++) {
                triangles[6*i+ind] = v+i;
                triangles[6*i+2+ind] = v+i+1;
                triangles[6*i+1+ind] = v+i+sphereSegments*2;
                triangles[6*i+ind+3] = v+i+sphereSegments*2;
                triangles[6*i+2+ind+3] = v+i+1;
                triangles[6*i+1+ind+3] = v+i+sphereSegments*2+1;
                if (i == sphereSegments*2-1) {
                    triangles[6*i+2+ind] = v+i+1-2*sphereSegments;
                    triangles[6*i+2+ind+3] = v+i+1-2*sphereSegments;
                    triangles[6*i+1+ind+3] = v+i+sphereSegments*2+1-2*sphereSegments;
                }
            }
        }

        v = nvec - sphereSegments*2;
        ind = ntri*3 - 3*sphereSegments*2;
        for (i=0; i < sphereSegments*2; i++) {
            triangles[3*i+ind] = 1;
            triangles[3*i+1+ind] = v+i+1;
            triangles[3*i+2+ind] = v+i;
            if (i==sphereSegments*2-1) {
                triangles[3*i+1+ind] = v;
            }
        }

        mesh.vertices = verts;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.Optimize();
    }

    private static void scaleDownToOne (ref Vector3 v)
    {
        float minC = 0.0f;
        if (v.x < minC)
            minC = v.x;
        if (v.y < minC)
            minC = v.y;
        if (v.z < minC)
            minC = v.z;
        v.x -= minC;
        v.y -= minC;
        v.z -= minC;
        
        float maxC = v.x;
        if (v.y > maxC)
            maxC = v.y;
        if (v.z > maxC)
            maxC = v.z;
        
        if (maxC > 1.0f) {
            v.x /= maxC;
            v.y /= maxC;
            v.z /= maxC;
        }
    }
	
	private void UpdateHosekWilkieCoefficients()
	{
	    int i;
	    double albedo = groundAlbedo;
	
	    for (int channel = 0; channel < 3; channel++)
	    {
	        int     int_turbidity = (int)T;
	        double  turbidity_rem = T - (double)int_turbidity;
	
	        Vector3 sunPos = ephemeris.GetSunPositionHorizon();
	        double solar_elevation = Math.Asin(sunPos.y);
	        if (solar_elevation < 0) solar_elevation = 0;
	
	        solar_elevation = Math.Pow(solar_elevation / (PI / 2.0), (1.0 / 3.0));
	
	        // alb 0 low turb
	
	        int offset = ( 9 * 6 * (int_turbidity-1) );
	    
	    
	        for( i = 0; i < 9; ++i )
	        {
	            //(1-t).^3* A1 + 3*(1-t).^2.*t * A2 + 3*(1-t) .* t .^ 2 * A3 + t.^3 * A4;
	            hosekWilkieCoeffs[channel,i] = 
	            (1.0-albedo) * (1.0 - turbidity_rem) 
	            * ( Math.Pow(1.0-solar_elevation, 5.0) * datasetsXYZ[channel][i + offset] + 
	               5.0  * Math.Pow(1.0-solar_elevation, 4.0) * solar_elevation * datasetsXYZ[channel][i + offset + 9] +
	               10.0*Math.Pow(1.0-solar_elevation, 3.0)*Math.Pow(solar_elevation, 2.0) * datasetsXYZ[channel][i + offset + 18] +
	               10.0*Math.Pow(1.0-solar_elevation, 2.0)*Math.Pow(solar_elevation, 3.0) * datasetsXYZ[channel][i + offset + 27] +
	               5.0*(1.0-solar_elevation)*Math.Pow(solar_elevation, 4.0) * datasetsXYZ[channel][i + offset + 36] +
	               Math.Pow(solar_elevation, 5.0)  * datasetsXYZ[channel][i + offset + 45]);
	        }
	
	        // alb 1 low turb
	        offset = (9*6*10 + 9*6*(int_turbidity-1));
	        for( i = 0; i < 9; ++i)
	        {
	            //(1-t).^3* A1 + 3*(1-t).^2.*t * A2 + 3*(1-t) .* t .^ 2 * A3 + t.^3 * A4;
	            hosekWilkieCoeffs[channel,i] += 
	            (albedo) * (1.0 - turbidity_rem)
	            * ( Math.Pow(1.0-solar_elevation, 5.0) * datasetsXYZ[channel][i + offset]  + 
	               5.0  * Math.Pow(1.0-solar_elevation, 4.0) * solar_elevation * datasetsXYZ[channel][i + offset + 9] +
	               10.0*Math.Pow(1.0-solar_elevation, 3.0)*Math.Pow(solar_elevation, 2.0) * datasetsXYZ[channel][i + offset + 18] +
	               10.0*Math.Pow(1.0-solar_elevation, 2.0)*Math.Pow(solar_elevation, 3.0) * datasetsXYZ[channel][i + offset + 27] +
	               5.0*(1.0-solar_elevation)*Math.Pow(solar_elevation, 4.0) * datasetsXYZ[channel][i + offset + 36] +
	               Math.Pow(solar_elevation, 5.0)  * datasetsXYZ[channel][i + offset + 45]);
	        }
	
	        if(int_turbidity < 10)
	        {
	            // alb 0 high turb
	            offset = (9*6*(int_turbidity));
	            for( i = 0; i < 9; ++i)
	            {
	                //(1-t).^3* A1 + 3*(1-t).^2.*t * A2 + 3*(1-t) .* t .^ 2 * A3 + t.^3 * A4;
	                hosekWilkieCoeffs[channel,i] += 
	                (1.0-albedo) * (turbidity_rem)
	                * ( Math.Pow(1.0-solar_elevation, 5.0) * datasetsXYZ[channel][i + offset]  + 
	                   5.0  * Math.Pow(1.0-solar_elevation, 4.0) * solar_elevation * datasetsXYZ[channel][i + offset + 9] +
	                   10.0*Math.Pow(1.0-solar_elevation, 3.0)*Math.Pow(solar_elevation, 2.0) * datasetsXYZ[channel][i + offset + 18] +
	                   10.0*Math.Pow(1.0-solar_elevation, 2.0)*Math.Pow(solar_elevation, 3.0) * datasetsXYZ[channel][i + offset + 27] +
	                   5.0*(1.0-solar_elevation)*Math.Pow(solar_elevation, 4.0) * datasetsXYZ[channel][i + offset + 36] +
	                   Math.Pow(solar_elevation, 5.0)  * datasetsXYZ[channel][i + offset + 45]);
	            }
	
	            // alb 1 high turb
	            offset = (9*6*10 + 9*6*(int_turbidity));
	            for( i = 0; i < 9; ++i)
	            {
	                //(1-t).^3* A1 + 3*(1-t).^2.*t * A2 + 3*(1-t) .* t .^ 2 * A3 + t.^3 * A4;
	                hosekWilkieCoeffs[channel,i] += 
	                (albedo) * (turbidity_rem)
	                * ( Math.Pow(1.0-solar_elevation, 5.0) * datasetsXYZ[channel][i + offset]  + 
	                   5.0  * Math.Pow(1.0-solar_elevation, 4.0) * solar_elevation * datasetsXYZ[channel][i + offset + 9] +
	                   10.0*Math.Pow(1.0-solar_elevation, 3.0)*Math.Pow(solar_elevation, 2.0) * datasetsXYZ[channel][i + offset + 18] +
	                   10.0*Math.Pow(1.0-solar_elevation, 2.0)*Math.Pow(solar_elevation, 3.0) * datasetsXYZ[channel][i + offset + 27] +
	                   5.0*(1.0-solar_elevation)*Math.Pow(solar_elevation, 4.0) * datasetsXYZ[channel][i + offset + 36] +
	                   Math.Pow(solar_elevation, 5.0)  * datasetsXYZ[channel][i + offset + 45]);
	            }
	        }
	
	        double res;
	
	        // alb 0 low turb
	        offset = (6*(int_turbidity-1));
	        //(1-t).^3* A1 + 3*(1-t).^2.*t * A2 + 3*(1-t) .* t .^ 2 * A3 + t.^3 * A4;
	        res = (1.0-albedo) * (1.0 - turbidity_rem) *
	            ( Math.Pow(1.0-solar_elevation, 5.0) * datasetsXYZRad[channel][offset + 0] +
	             5.0*Math.Pow(1.0-solar_elevation, 4.0)*solar_elevation * datasetsXYZRad[channel][offset + 1] +
	             10.0*Math.Pow(1.0-solar_elevation, 3.0)*Math.Pow(solar_elevation, 2.0) * datasetsXYZRad[channel][offset + 2] +
	             10.0*Math.Pow(1.0-solar_elevation, 2.0)*Math.Pow(solar_elevation, 3.0) * datasetsXYZRad[channel][offset + 3] +
	             5.0*(1.0-solar_elevation)*Math.Pow(solar_elevation, 4.0) * datasetsXYZRad[channel][offset + 4] +
	             Math.Pow(solar_elevation, 5.0) * datasetsXYZRad[channel][offset + 5]);
	
	        // alb 1 low turb
	        offset = (6*10 + 6*(int_turbidity-1));
	        //(1-t).^3* A1 + 3*(1-t).^2.*t * A2 + 3*(1-t) .* t .^ 2 * A3 + t.^3 * A4;
	        res += (albedo) * (1.0 - turbidity_rem) *
	            ( Math.Pow(1.0-solar_elevation, 5.0) * datasetsXYZRad[channel][offset + 0] +
	             5.0*Math.Pow(1.0-solar_elevation, 4.0)*solar_elevation * datasetsXYZRad[channel][offset + 1] +
	             10.0*Math.Pow(1.0-solar_elevation, 3.0)*Math.Pow(solar_elevation, 2.0) * datasetsXYZRad[channel][offset + 2] +
	             10.0*Math.Pow(1.0-solar_elevation, 2.0)*Math.Pow(solar_elevation, 3.0) * datasetsXYZRad[channel][offset + 3] +
	             5.0*(1.0-solar_elevation)*Math.Pow(solar_elevation, 4.0) * datasetsXYZRad[channel][offset + 4] +
	             Math.Pow(solar_elevation, 5.0) * datasetsXYZRad[channel][offset + 5]);
	
	        if(int_turbidity < 10)
	        {
	            // alb 0 high turb
	            offset = (6*(int_turbidity));
	            //(1-t).^3* A1 + 3*(1-t).^2.*t * A2 + 3*(1-t) .* t .^ 2 * A3 + t.^3 * A4;
	            res += (1.0-albedo) * (turbidity_rem) *
	                ( Math.Pow(1.0-solar_elevation, 5.0) * datasetsXYZRad[channel][offset + 0] +
	                 5.0*Math.Pow(1.0-solar_elevation, 4.0)*solar_elevation * datasetsXYZRad[channel][offset + 1] +
	                 10.0*Math.Pow(1.0-solar_elevation, 3.0)*Math.Pow(solar_elevation, 2.0) * datasetsXYZRad[channel][offset + 2] +
	                 10.0*Math.Pow(1.0-solar_elevation, 2.0)*Math.Pow(solar_elevation, 3.0) * datasetsXYZRad[channel][offset + 3] +
	                 5.0*(1.0-solar_elevation)*Math.Pow(solar_elevation, 4.0) * datasetsXYZRad[channel][offset + 4] +
	                 Math.Pow(solar_elevation, 5.0) * datasetsXYZRad[channel][offset + 5]);
	
	            // alb 1 high turb
	            offset = (6*10 + 6*(int_turbidity));
	            //(1-t).^3* A1 + 3*(1-t).^2.*t * A2 + 3*(1-t) .* t .^ 2 * A3 + t.^3 * A4;
	            res += (albedo) * (turbidity_rem) *
	                ( Math.Pow(1.0-solar_elevation, 5.0) * datasetsXYZRad[channel][offset + 0] +
	                 5.0*Math.Pow(1.0-solar_elevation, 4.0)*solar_elevation * datasetsXYZRad[channel][offset + 1] +
	                 10.0*Math.Pow(1.0-solar_elevation, 3.0)*Math.Pow(solar_elevation, 2.0) * datasetsXYZRad[channel][offset + 2] +
	                 10.0*Math.Pow(1.0-solar_elevation, 2.0)*Math.Pow(solar_elevation, 3.0) * datasetsXYZRad[channel][offset + 3] +
	                 5.0*(1.0-solar_elevation)*Math.Pow(solar_elevation, 4.0) * datasetsXYZRad[channel][offset + 4] +
	                 Math.Pow(solar_elevation, 5.0) * datasetsXYZRad[channel][offset + 5]);
	        }
	       
	        hosekWilkieRadiances[channel] = res;
	    }
	}

	
    private void UpdateLight ()
    {
        const float epsilon = 1E-7f;
        Vector3 sunXYZ = sunTransmittedLuminance * (float)sunLuminanceScale;
        
        SilverLiningLuminanceMapper.DurandMapperXYZ (ref sunXYZ);
        
        Vector3 rgb = sunXYZ * XYZ2RGB;
        
        ApplyGamma (ref rgb);
        
        Color sunLightColor = new Color (rgb.x, rgb.y, rgb.z);
        
        Transform trans = sunLight.GetComponent<Transform> ();
        Vector3 sunPos = trans.position;
        trans.LookAt (sunPos - ephemeris.GetSunPositionHorizon ());
        
        Light light = sunLight.GetComponent<Light> ();
        light.color = sunLightColor * sunLightScale;
        light.enabled = light.color.g > epsilon;
        
        Vector3 moonXYZ = moonTransmittedLuminance * (float)moonLuminanceScale;
        
        SilverLiningLuminanceMapper.DurandMapperXYZ (ref moonXYZ);
        
        rgb = moonXYZ * XYZ2RGB;
        
        ApplyGamma (ref rgb);
        
        Color moonLightColor = new Color (rgb.x, rgb.y, rgb.z);
        
        trans = moonLight.GetComponent<Transform> ();
        Vector3 moonPos = trans.position;
        trans.LookAt (moonPos - ephemeris.GetMoonPositionHorizon ());
        
        light = moonLight.GetComponent<Light> ();
        light.color = moonLightColor * moonLightScale;
        light.enabled = light.color.g > epsilon;
        
        int moonDay = (int)(Math.Floor ((ephemeris.GetMoonPhaseAngle () / (2.0 * PI)) * 30.0));
        
        RenderSettings.ambientLight = skyLight * ambientLightScale;
    }

    private void UpdateMoon ()
    {
        Vector3 moonPosHorizon = ephemeris.GetMoonPositionHorizon ();
        moonPosHorizon.Normalize ();
        moonPosHorizon *= moonDistance;
        
        Transform trans = moon.GetComponent<Transform> ();
        trans.position = moonPosHorizon + Camera.main.transform.position;
			
        Vector3 moonXYZ = moonTransmittedLuminance;
        Vector3 moonColorV = moonXYZ * XYZ2RGB;
        scaleDownToOne (ref moonColorV);
        
        moonColorV.Normalize ();
        
        if (moonColorV.x < 0)
            moonColorV.x = 0;
        if (moonColorV.y < 0)
            moonColorV.y = 0;
        if (moonColorV.z < 0)
            moonColorV.z = 0;
        
        ApplyGamma (ref moonColorV);
        
        Vector3 one = new Vector3 (1, 1, 1);
        moonColorV = moonColorV * (float)isothermalEffect + one * (1.0f - (float)isothermalEffect);
        
        Color moonColor = new Color (moonColorV.x, moonColorV.y, moonColorV.z);
        
    }

    private void UpdateSun ()
    {
        Vector3 sunPosHorizon = ephemeris.GetSunPositionHorizon ();
        sunPosHorizon.Normalize ();
        sunPosHorizon *= sunDistance;
        
        Transform trans = sun.GetComponent<Transform> ();
        trans.position = sunPosHorizon + Camera.main.transform.position;
        
        Vector3 sunXYZ = sunTransmittedLuminance;
        Vector3 sunColorV = sunXYZ * XYZ2RGB;
        scaleDownToOne (ref sunColorV);
        
        sunColorV.Normalize ();
        
        if (sunColorV.x < 0)
            sunColorV.x = 0;
        if (sunColorV.y < 0)
            sunColorV.y = 0;
        if (sunColorV.z < 0)
            sunColorV.z = 0;
        
        ApplyGamma (ref sunColorV);
        
        Vector3 one = new Vector3 (1, 1, 1);
        sunColorV = sunColorV * (float)isothermalEffect + one * (1.0f - (float)isothermalEffect);
        
        Color sunColor = new Color (sunColorV.x, sunColorV.y, sunColorV.z);
        
    }

    private void ComputeLogAvg ()
    {
        double Y, R;
        Vector3 scatteredLuminance;
        
        scatteredLuminance = sunScatteredLuminance + moonScatteredLuminance + moonTransmittedLuminance;
        
        scatteredLuminance.y += (float)NightSkyLuminance () * 1000.0f;
        
        Y = scatteredLuminance.y;
        R = -0.702 * scatteredLuminance.x + 1.039 * scatteredLuminance.y + 0.433 * scatteredLuminance.z;
        
        SilverLiningLuminanceMapper.SetSceneLogAvg (R, Y);
    }

    private void ComputeToneMappedSkyLight ()
    {
        Vector3 sunXYZ = sunScatteredLuminance * (float)sunLuminanceScale;
        sunXYZ.y += (float)NightSkyLuminance () * 1000.0f;
        
        Vector3 moonXYZ = moonScatteredLuminance * (float)moonLuminanceScale;
        
                /*
        if (Atmosphere::GetHDREnabled())
        {
            // convert to kCd
            sunXYZ = sunXYZ * 0.001;
            moonXYZ = moonXYZ * 0.001;
        }
        else */
        {
            SilverLiningLuminanceMapper.DurandMapperXYZ (ref sunXYZ);
            SilverLiningLuminanceMapper.DurandMapperXYZ (ref moonXYZ);
        }
        
        Vector3 XYZ = (sunXYZ + moonXYZ);
        
//      if (!Atmosphere::GetHDREnabled())
        {
            if (XYZ.x > (float)maxSkylightLuminance)
                XYZ.x = (float)maxSkylightLuminance;
            if (XYZ.y > (float)maxSkylightLuminance)
                XYZ.y = (float)maxSkylightLuminance;
            if (XYZ.z > (float)maxSkylightLuminance)
                XYZ.z = (float)maxSkylightLuminance;
        }
        
        Vector3 rgb = XYZ * XYZ2RGB;
        
        //scaleDownToOne(rgb);
        ApplyGamma (ref rgb);
        
        skyLight = new Color (rgb.y, rgb.y, rgb.y);
    }

    private void ApplyGamma (ref Vector3 v)
    {
        float min = 0;
        if (v.x < min)
            min = v.x;
        if (v.y < min)
            min = v.y;
        if (v.z < min)
            min = v.z;
        min = -min;
        
        v.x = v.x + min;
        v.y = v.y + min;
        v.z = v.z + min;
        
        //if (!Atmosphere::GetHDREnabled())
        {
            float max = v.x;
            if (v.y > max)
                max = v.y;
            if (v.z > max)
                max = v.z;
            if (max > 1.0f) {
                v.x /= max;
                v.y /= max;
                v.z /= max;
            }
            
            if (v.x > 0.0f)
                v.x = (float)Math.Pow (v.x, oneOverGamma);
            if (v.y > 0.0f)
                v.y = (float)Math.Pow (v.y, oneOverGamma);
            if (v.z > 0.0f)
                v.z = (float)Math.Pow (v.z, oneOverGamma);
        }
    }

    private double PerezY (double theta, double gamma)
    {
        return (1.0 + AY * Math.Exp (BY / Math.Cos (theta))) * (1.0 + CY * Math.Exp (DY * gamma) + EY * Math.Cos (gamma) * Math.Cos (gamma));
    }

    private double Perezx (double theta, double gamma)
    {
        return (1.0 + Ax * Math.Exp (Bx / Math.Cos (theta))) * (1.0 + Cx * Math.Exp (Dx * gamma) + Ex * Math.Cos (gamma) * Math.Cos (gamma));
        
    }

    private double Perezy (double theta, double gamma)
    {
        return (1.0 + Ay * Math.Exp (By / Math.Cos (theta))) * (1.0 + Cy * Math.Exp (Dy * gamma) + Ey * Math.Cos (gamma) * Math.Cos (gamma));
    }
	
	private double HosekWilkie(int channel, double theta, double gamma)
	{
	    double expM = Math.Exp(hosekWilkieCoeffs[channel,4] * gamma);
	    double rayM = Math.Cos(gamma)*Math.Cos(gamma);
	    double mieM = (1.0 + Math.Cos(gamma)*Math.Cos(gamma)) / Math.Pow((1.0 + hosekWilkieCoeffs[channel,8]*hosekWilkieCoeffs[channel,8] - 2.0*hosekWilkieCoeffs[channel,8]*Math.Cos(gamma)), 1.5);
	     double zenith = Math.Sqrt(Math.Cos(theta));
	
	    return (1.0 + hosekWilkieCoeffs[channel,0] * Math.Exp(hosekWilkieCoeffs[channel,1] / (Math.Cos(theta) + 0.01))) *
	            (hosekWilkieCoeffs[channel,2] + hosekWilkieCoeffs[channel,3] * expM + hosekWilkieCoeffs[channel,5] * rayM + hosekWilkieCoeffs[channel,6] * mieM + hosekWilkieCoeffs[channel,7] * zenith);
	}
	
	private double HosekWilkieX(double theta, double gamma)
	{
	    return HosekWilkie(0, theta, gamma);
	}
	
	private double HosekWilkieY(double theta, double gamma)
	{
	    return HosekWilkie(1, theta, gamma);
	}
	
	private double HosekWilkieZ(double theta, double gamma)
	{
	    return HosekWilkie(2, theta, gamma);
	}

    private double AngleBetween (Vector3 v1, Vector3 v2)
    {
        Vector3 a = v1;
        a.Normalize ();
        Vector3 b = v2;
        b.Normalize ();
        
        double dot = Vector3.Dot (a, b);
        
        return Math.Acos (dot);
    }

    private double NightSkyLuminance ()
    {
        // Bright planets
        // Zodiacal light
        // Integrated starlight
        // Airglow
        // Diffuse galactic light
        double Wm2 = lightPollution + 2.0E-6 + 1.2E-7 + 3.0E-8 + 5.1E-8 + 9.1E-9 + 9.1E-10;
        // Cosmic light
        double nits = NITS (Wm2);
        
        return nits * isothermalEffect * 0.001;
    }

    private void UpdateZenith (double altitude)
    {
        Vector3 sunPos = ephemeris.GetSunPositionHorizon ();
        Vector3 zenithPos = new Vector3 (0, 1, 0);
        thetaS = AngleBetween (zenithPos, sunPos);
        
        Vector3 moonPos = ephemeris.GetMoonPositionHorizon ();
        thetaM = AngleBetween (zenithPos, moonPos);
        
        double den = sunScatteredLuminance.x + sunScatteredLuminance.y + sunScatteredLuminance.z;
        double denMoon = moonScatteredLuminance.x + moonScatteredLuminance.y + moonScatteredLuminance.z;
        
        xZenith = yZenith = xMoon = yMoon = 0.2;
        
        if (den != 0.0) {
            xZenith = sunScatteredLuminance.x / den;
            yZenith = sunScatteredLuminance.y / den;
        }
        
                /*
            // Zenith chromaticity from "A Practical Analytic Model for Daylight"
            double theta3 = thetaS * thetaS * thetaS;
            double theta2 = thetaS * thetaS;
            double T2 = T * T;
            xZenith =
           ( 0.00165 * theta3 - 0.00375 * theta2 + 0.00209 * thetaS + 0.0)     * T2 +
           (-0.02903 * theta3 + 0.06377 * theta2 - 0.03202 * thetaS + 0.00394) * T +
           ( 0.11693 * theta3 - 0.21196 * theta2 + 0.06052 * thetaS + 0.25886);
    
             yZenith =
           ( 0.00275 * theta3 - 0.00610 * theta2 + 0.00317 * thetaS + 0.0)     * T2 +
           (-0.04214 * theta3 + 0.08970 * theta2 - 0.04153 * thetaS + 0.00516) * T +
           ( 0.15346 * theta3 - 0.26756 * theta2 + 0.06670 * thetaS + 0.26688);
         */

        if (denMoon != 0.0) {
            xMoon = moonScatteredLuminance.x / denMoon;
            yMoon = moonScatteredLuminance.y / denMoon;
        }
        
        YMoon = moonScatteredLuminance.y * 0.001 * moonScale;
        
        // Assume that our own scattered sunlight calculation is the zenith luminance.
        YZenith = sunScatteredLuminance.y * 0.001 + NightSkyLuminance ();
        
        // Account for high altitude. As you lose atmosphere, less scattering occurs.
        H *= SilverLining.unitScale;
        
        isothermalEffect = Math.Exp (-(altitude / H));
        if (isothermalEffect < 0)
            isothermalEffect = 0;
        if (isothermalEffect > 1.0)
            isothermalEffect = 1.0;
        YZenith *= isothermalEffect;
        YMoon *= isothermalEffect;
        
        // Alternate approaches:
        
        // Zenith luminance from "A Practical Analytic Model for Daylight" (Preeham, Shirley Smits)
        // double chiSun = (4.0/9.0 - T / 120.0) * (PI - 2 * thetaS);
        // YZenith = (4.053 * T - 4.9710) * tan(chiSun) - 0.2155 * T + 2.4192 + NightSkyLuminance();
        
        // Zenith luminance from "Sky Luminance Distribution Model for Simulation of Daylit
        // Environment" (Igawa, Nakamura, Matsuura)
    }
        /*
           double Ys = (PI * 0.5) - thetaS;
    
           double A = 18.373 * Ys + 9.955;
           double B = -52.013 * Ys - 37.766;
           double C = 46.572 * Ys + 59.352;
           double D = 1.691 * Ys * Ys - 16.498 * Ys - 48.670;
           double E = 1.124 * Ys + 19.738;
           double F = 1.170 * log(Ys) + 6.369;
    
           const double N = 1.0; // normalized global illuminance
           YZenith = exp(A * N * N * N * N * N +
                B * N * N * N * N +
             C * N * N * N +
             D * N * N +
             E * N +
             F) * 0.001 + NightSkyLuminance(); */        
        
    
    private void UpdatePerezCoefficients ()
    {
        if (T != lastT) {
            lastT = T;
            
            AY = (0.1787 * T - 1.4630) * aScale;
            BY = (-0.3554 * T + 0.4275) * bScale;
            CY = (-0.0227 * T + 5.3251) * cScale;
            DY = (0.1206 * T - 2.5771) * dScale;
            EY = (-0.0670 * T + 0.3702) * eScale;
            
            Ax = -0.0193 * T - 0.2592;
            Bx = -0.0665 * T + 0.0008;
            Cx = -0.0004 * T + 0.2125;
            Dx = -0.0641 * T - 0.8989;
            Ex = -0.0033 * T + 0.0452;
            
            Ay = -0.0167 * T - 0.2608;
            By = -0.0950 * T + 0.0092;
            Cy = -0.0079 * T + 0.2102;
            Dy = -0.0441 * T - 1.6537;
            Ey = -0.0109 * T + 0.0529;
        }
    }

    private void ComputeSun (double altitude)
    {
        Vector3 sunPos = ephemeris.GetSunPositionHorizon ();
        sunPos.Normalize ();
        double cosZenith = sunPos.y;
        
        if (lastSunT != T || lastSunZenith != cosZenith) {
            lastSunT = T;
            lastSunZenith = cosZenith;
            
            lightingChanged = true;
			
			double solarAltitude = DEGREES(Math.Asin(sunPos.y));
			if (solarAltitude > 1.0)
		    {
		        perezBlend = 0;
		    } else if (solarAltitude < 0) {
		        perezBlend = 1.0f;
		    } else {
		        perezBlend = 1.0f - (float)solarAltitude;
		    }
			
            
            if (cosZenith > 0) {
                double zenithAngle = Math.Acos (cosZenith);
                
                SilverLiningSpectrum solarDirect = new SilverLiningSpectrum ();
                SilverLiningSpectrum solarScattered = new SilverLiningSpectrum ();
                
                sunSpectrum.ApplyAtmosphericTransmittance (zenithAngle, cosZenith, T, altitude, ref solarDirect, ref solarScattered);
                
                sunTransmittedLuminance = solarDirect.ToXYZ ();
                sunScatteredLuminance = solarScattered.ToXYZ ();
                
                // Apply sunset color tweaks
                double alpha = zenithAngle / (PI * 0.5);
                for (int i = 0; i < boostExp; i++)
                    alpha *= alpha;
                sunScatteredLuminance.x *= (float)(1.0 + alpha * XBoost);
                sunScatteredLuminance.y *= (float)(1.0 + alpha * YBoost);
                sunScatteredLuminance.z *= (float)(1.0 + alpha * ZBoost);
            } else {
                // In twilight conditions, we lookup luminance based on experimental results
                // on cloudless nights.
                
                int lower = (int)Math.Floor (solarAltitude);
                int higher = (int)Math.Ceiling (solarAltitude);
                
                float alpha = (float)(solarAltitude - lower);
                
                float a = 0;
                float b = 0;
                if (lower >= -16 && higher >= -16) {
                    a = twilightLuminance[lower];
                    b = twilightLuminance[higher];
                }
                
                // Blend light from sunset
                const double epsilon = 0.001;
                SilverLiningSpectrum solarDirect = new SilverLiningSpectrum ();
                SilverLiningSpectrum solarScattered = new SilverLiningSpectrum ();
                
                double zenithAngle = PI * 0.5 - epsilon;
                sunSpectrum.ApplyAtmosphericTransmittance (zenithAngle, Math.Cos (zenithAngle), T, altitude, ref solarDirect, ref solarScattered);
                sunTransmittedLuminance = solarDirect.ToXYZ ();
                sunScatteredLuminance = solarScattered.ToXYZ ();
                
                float Y = (1 - alpha) * a + alpha * b;
                // luminance per lookup table
                float x = 0.25f;
                float y = 0.25f;
                float minDirectional = 0.1f;
                
                float X = x * (Y / y);
                float Z = (1.0f - x - y) * (Y / y);
                
                alpha = -(float)solarAltitude / 2.0f;
                if (alpha > 1.0f)
                    alpha = 1.0f;
                if (alpha < 0)
                    alpha = 0;
                alpha = alpha * alpha;
                sunTransmittedLuminance = sunTransmittedLuminance * Y * minDirectional * alpha + sunTransmittedLuminance * (1.0f - alpha);
                Vector3 twilight = new Vector3 (X, Y, Z);
                sunScatteredLuminance = twilight * alpha + sunScatteredLuminance * (1.0f - alpha);
                
                // Apply sunset color tweaks
                sunScatteredLuminance.x *= 1.0f + XBoost;
                sunScatteredLuminance.y *= 1.0f + YBoost;
                sunScatteredLuminance.z *= 1.0f + ZBoost;
            }
            
            if (isOvercast) {
                sunTransmittedLuminance = (sunTransmittedLuminance * (overcastBlend * overcastTransmission)) + (sunTransmittedLuminance * (1.0f - overcastBlend));
                sunScatteredLuminance = (sunScatteredLuminance * (overcastBlend * overcastTransmission)) + (sunScatteredLuminance * (1.0f - overcastBlend));
            }
            
            
            sunTransmittedLuminance = sunTransmittedLuminance * sunTransmissionScale;
            sunScatteredLuminance = sunScatteredLuminance * sunScatteredScale;
        }
    }

    private void ComputeMoon (double altitude)
    {
        Vector3 moonPos = ephemeris.GetMoonPositionHorizon ();
        moonPos.Normalize ();
        
        double cosZenith = moonPos.y;
        double zenith = Math.Acos (cosZenith);
        
        if (lastMoonT != T || lastMoonZenith != zenith) {
            lastMoonT = T;
            lastMoonZenith = zenith;
            
            lightingChanged = true;
            
            SilverLiningSpectrum moonSpectrumEarthDirect = new SilverLiningLunarSpectrum ();
            SilverLiningSpectrum moonSpectrumEarthScattered = new SilverLiningLunarSpectrum ();
            
            lunarSpectrum.ApplyAtmosphericTransmittance (zenith, cosZenith, T, altitude, ref moonSpectrumEarthDirect, ref moonSpectrumEarthScattered);
            float moonLuminance = MoonLuminance ();
            moonTransmittedLuminance = moonSpectrumEarthDirect.ToXYZ () * moonLuminance;
            moonScatteredLuminance = moonSpectrumEarthScattered.ToXYZ () * moonLuminance;
            
            if (isOvercast) {
                moonTransmittedLuminance = (moonTransmittedLuminance * (overcastBlend * overcastTransmission)) + (moonTransmittedLuminance * (1.0f - overcastBlend));
                moonScatteredLuminance = (moonScatteredLuminance * (overcastBlend * overcastTransmission)) + (moonScatteredLuminance * (1.0f - overcastBlend));
            }
            
            moonTransmittedLuminance = moonTransmittedLuminance * moonTransmissionScale;
            moonScatteredLuminance = moonScatteredLuminance * moonScatteredScale;
            
        }
    }

    private float MoonLuminance ()
    {
        float luminance = 0;
        
        if (ephemeris != null) {
            Vector3 moonPos = ephemeris.GetMoonPositionHorizon ();
            moonPos.Normalize ();
            double moonAngle = DEGREES (Math.Asin (moonPos.y));
            
            //if (moonAngle > -18)
            {
                const double Esm = 1905.0;
                // W/m2
                const double C = 0.072;
                const double Rm = 1738.1 * 1000.0;
                // m
                double d = ephemeris.GetMoonDistanceKM () * 1000.0;
                
                // The equations for the illumination from the moon below assume that
                // the moon phase angle is 1 when full and 0 when new, which is the
                // opposite of the convention assumed by the Ephemeris class. So,
                // we assign the Earth's phase to the moon phase angle (which is always
                // its opposite) and take the opposite of that to determine the moon
                // phase angle for the purposes of these calculations.
                
                double epsilon = 0.001;
                
                double ePhase = ephemeris.GetMoonPhaseAngle ();
                if (ePhase < epsilon)
                    ePhase = epsilon;
                
                double alpha = PI - ePhase;
                while (alpha < 0) {
                    alpha += 2.0 * PI;
                }
                if (alpha < epsilon)
                    alpha = epsilon;
                
                // Earthshine:
                double Eem = 0.19 * 0.5 * (1.0 - Math.Sin (ePhase / 2.0) * Math.Tan (ePhase / 2.0) * Math.Log (1.0 / Math.Tan (ePhase / 4.0)));
                
                // Total moonlight:
                double Em = ((2.0 * C * Rm * Rm) / (3.0 * d * d)) * (Eem + Esm * (1.0 - Math.Sin (alpha / 2.0) * Math.Tan (alpha / 2.0) * Math.Log (1.0 / Math.Tan (alpha / 4.0))));
                
                double nits = NITS (Em);
                
                nits *= 0.001;
                
                if (moonAngle < 0) {
                    nits = nits * Math.Exp (1.1247 * moonAngle);
                }
                
                luminance = (float)nits;
            }
        }
        
        return luminance;
    }

    private void InitTwilightLuminances ()
    {
        twilightLuminance = new Dictionary<int, float> ();
        
        twilightLuminance[5] = 2200.0f / PIf;
        twilightLuminance[4] = 1800.0f / PIf;
        twilightLuminance[3] = 1400.0f / PIf;
        twilightLuminance[2] = 1200.0f / PIf;
        twilightLuminance[1] = 710.0f / PIf;
        twilightLuminance[0] = 400.0f / PIf;
        twilightLuminance[-1] = 190.0f / PIf;
        twilightLuminance[-2] = 77.0f / PIf;
        twilightLuminance[-3] = 28.0f / PIf;
        twilightLuminance[-4] = 9.4f / PIf;
        twilightLuminance[-5] = 2.9f / PIf;
        twilightLuminance[-6] = 0.9f / PIf;
        twilightLuminance[-7] = 0.3f / PIf;
        twilightLuminance[-8] = 0.11f / PIf;
        twilightLuminance[-9] = 0.047f / PIf;
        twilightLuminance[-10] = 0.021f / PIf;
        twilightLuminance[-11] = 0.0092f / PIf;
        twilightLuminance[-12] = 0.0031f / PIf;
        twilightLuminance[-13] = 0.0022f / PIf;
        twilightLuminance[-14] = 0.0019f / PIf;
        twilightLuminance[-15] = 0.0018f / PIf;
        twilightLuminance[-16] = 0.0018f / PIf;
    }

    public bool GetLightingChanged ()
    {
        return lightingChanged;
    }

    public Vector3 GetSunOrMoonPosition ()
    {
        if (ephemeris != null) {
            if (sunTransmittedLuminance.sqrMagnitude > moonTransmittedLuminance.sqrMagnitude) {
                return ephemeris.GetSunPositionHorizon ();
            } else {
                return ephemeris.GetMoonPositionHorizon ();
            }
        } else {
            return new Vector3 (0, 1, 0);
        }
    }

    public Color GetSunOrMoonColor ()
    {
        Vector3 sunXYZ = sunTransmittedLuminance * (float)sunLuminanceScale;
        Vector3 moonXYZ = moonTransmittedLuminance * (float)moonLuminanceScale;
        
        SilverLiningLuminanceMapper.DurandMapperXYZ (ref sunXYZ);
        SilverLiningLuminanceMapper.DurandMapperXYZ (ref moonXYZ);
        
        Vector3 XYZ = (sunXYZ + moonXYZ);
        
        Vector3 rgb = XYZ * XYZ2RGB;
        
        ApplyGamma (ref rgb);
        
        return new Color (rgb.x, rgb.y, rgb.z);
    }

    private SilverLiningEphemeris ephemeris;
    private Dictionary<int, float> twilightLuminance;

    public double T = 2.2;
    private double lastT = 0;
    private double lastSunT = 0, lastMoonT = 0;
    private double lastSunZenith = 0, lastMoonZenith = 0;
    private bool lightingChanged = true;

    private double AY, BY, CY, DY, EY;
    // Perez luminance coefficients
    private double Ax, Bx, Cx, Dx, Ex;
    // Perez chromaticity coefficients
    private double Ay, By, Cy, Dy, Ey;

    private double thetaS;
    // Angle between sun and zenith
    private double thetaM;
    // Angle between moon and zenith
    private double xZenith, yZenith, YZenith, xMoon, yMoon, YMoon;

    private double sunx, suny, sunY;
    private double moonx, moony, moonY;

    public double maxSkylightLuminance = 1.0;

    private Color skyLight;
    private Vector3 sunTransmittedLuminance, moonTransmittedLuminance;
    private Vector3 sunScatteredLuminance, moonScatteredLuminance;

    private SilverLiningSpectrum sunSpectrum, lunarSpectrum;

    private bool isOvercast = false;
    private float overcastBlend = 1.0f, overcastTransmission = 0.2f;
    public double lightPollution = 0;
    private double isothermalEffect = 1.0;

    private SilverLiningMatrix3 XYZ2RGB;
    private Matrix4x4 XYZ2RGB4;

    private GameObject sun;
    private GameObject sunLight;

    private GameObject moon;
    private GameObject moonLight;
    private Texture[] moonTextures;
	
	// Hosek-Wilkie coefficients [channel][constant A-I]
    private double[,] hosekWilkieCoeffs;
    private double[] hosekWilkieRadiances;

    private Shader starFogShader, starNoFogShader;

    private SilverLiningStars stars;

    public float sunLightScale = 1.0f, moonLightScale = 1.0f, ambientLightScale = 1.0f;
	
	private float perezBlend;
	

double[] datasetXYZ1 = new double[]
{
	// albedo 0, turbidity 1
	-1.117001e+000,
	-1.867262e-001,
	-1.113505e+001,
	1.259865e+001,
	-3.937339e-002,
	1.167571e+000,
	7.100686e-003,
	3.592678e+000,
	6.083296e-001,
	-1.152006e+000,
	-1.926669e-001,
	6.152049e+000,
	-4.770802e+000,
	-8.704701e-002,
	7.483626e-001,
	3.372718e-002,
	4.464592e+000,
	4.036546e-001,
	-1.072371e+000,
	-2.696632e-001,
	2.816168e-001,
	1.820571e+000,
	-3.742666e-001,
	2.080607e+000,
	-7.675295e-002,
	-2.835366e+000,
	1.129329e+000,
	-1.109935e+000,
	-1.532764e-001,
	1.198787e+000,
	-9.015183e-001,
	5.173015e-003,
	5.749178e-001,
	1.075633e-001,
	4.387949e+000,
	2.650413e-001,
	-1.052297e+000,
	-2.229452e-001,
	1.952347e+000,
	5.727205e-001,
	-4.885070e+000,
	1.984016e+000,
	-1.106197e-001,
	-4.898361e-001,
	8.907873e-001,
	-1.070108e+000,
	-1.600465e-001,
	1.593886e+000,
	-4.479251e-005,
	-3.306541e+000,
	9.390193e-001,
	9.513168e-002,
	2.343583e+000,
	5.335404e-001,
	// albedo 0, turbidity 2
	-1.113253e+000,
	-1.699600e-001,
	-1.038822e+001,
	1.137513e+001,
	-4.040911e-002,
	1.037455e+000,
	4.991792e-002,
	4.801919e+000,
	6.302710e-001,
	-1.135747e+000,
	-1.678594e-001,
	4.970755e+000,
	-4.430230e+000,
	-6.657408e-002,
	3.636161e-001,
	1.558009e-001,
	6.013370e+000,
	3.959601e-001,
	-1.095892e+000,
	-2.732595e-001,
	7.666496e-001,
	1.350731e+000,
	-4.401401e-001,
	2.470135e+000,
	-1.707929e-001,
	-3.260793e+000,
	1.170337e+000,
	-1.073668e+000,
	-2.603929e-002,
	-1.944589e-001,
	4.575207e-001,
	6.878164e-001,
	-1.390770e-001,
	3.690299e-001,
	7.885781e+000,
	1.877694e-001,
	-1.070091e+000,
	-2.798957e-001,
	2.338478e+000,
	-2.647221e+000,
	-7.387808e+000,
	2.329210e+000,
	-1.644639e-001,
	-2.003710e+000,
	9.874527e-001,
	-1.067120e+000,
	-1.418866e-001,
	1.254090e+000,
	6.053048e+000,
	-2.918892e+000,
	5.322812e-001,
	1.613053e-001,
	3.018161e+000,
	5.274090e-001,
	// albedo 0, turbidity 3
	-1.129483e+000,
	-1.890619e-001,
	-9.065101e+000,
	9.659923e+000,
	-3.607819e-002,
	8.314359e-001,
	8.181661e-002,
	4.768868e+000,
	6.339777e-001,
	-1.146420e+000,
	-1.883579e-001,
	3.309173e+000,
	-3.127882e+000,
	-6.938176e-002,
	3.987113e-001,
	1.400581e-001,
	6.283042e+000,
	5.267076e-001,
	-1.128348e+000,
	-2.641305e-001,
	1.223176e+000,
	5.514952e-002,
	-3.490649e-001,
	1.997784e+000,
	-4.123709e-002,
	-2.251251e+000,
	9.483466e-001,
	-1.025820e+000,
	1.404690e-002,
	-1.187406e+000,
	2.729900e+000,
	5.877588e-001,
	-2.761140e-001,
	4.602633e-001,
	8.305125e+000,
	3.945001e-001,
	-1.083957e+000,
	-2.606679e-001,
	2.207108e+000,
	-7.202803e+000,
	-5.968103e+000,
	2.129455e+000,
	-7.789512e-002,
	-1.137688e+000,
	8.871769e-001,
	-1.062465e+000,
	-1.512189e-001,
	1.042881e+000,
	1.427839e+001,
	-4.242214e+000,
	4.038100e-001,
	1.997780e-001,
	2.814449e+000,
	5.803196e-001,
	// albedo 0, turbidity 4
	-1.175099e+000,
	-2.410789e-001,
	-1.108587e+001,
	1.133404e+001,
	-1.819300e-002,
	6.772942e-001,
	9.605043e-002,
	4.231166e+000,
	6.239972e-001,
	-1.224207e+000,
	-2.883527e-001,
	3.002206e+000,
	-2.649612e+000,
	-4.795418e-002,
	4.984398e-001,
	3.251434e-002,
	4.851611e+000,
	6.551019e-001,
	-1.136955e+000,
	-2.423048e-001,
	1.058823e+000,
	-2.489236e-001,
	-2.462179e-001,
	1.933140e+000,
	9.106828e-002,
	-1.905869e-001,
	8.171065e-001,
	-1.014535e+000,
	-8.262500e-003,
	-1.448017e+000,
	2.295788e+000,
	3.510334e-001,
	-1.477418e+000,
	5.432449e-001,
	5.762796e+000,
	4.908751e-001,
	-1.070666e+000,
	-2.379780e-001,
	1.844589e+000,
	-5.442448e+000,
	-4.012768e+000,
	2.945275e+000,
	9.854725e-003,
	8.455959e-002,
	8.145030e-001,
	-1.071525e+000,
	-1.777132e-001,
	8.076590e-001,
	9.925865e+000,
	-3.324623e+000,
	-6.367437e-001,
	2.844581e-001,
	2.248384e+000,
	6.544022e-001,
	// albedo 0, turbidity 5
	-1.218818e+000,
	-2.952382e-001,
	-1.345975e+001,
	1.347153e+001,
	-6.814585e-003,
	5.079068e-001,
	1.197230e-001,
	3.776949e+000,
	5.836961e-001,
	-1.409868e+000,
	-5.114330e-001,
	2.776539e+000,
	-2.039001e+000,
	-2.673769e-002,
	4.145288e-001,
	7.829342e-004,
	2.275883e+000,
	6.629691e-001,
	-1.069151e+000,
	-9.434247e-002,
	7.293972e-001,
	-1.222473e+000,
	-1.533461e-001,
	2.160357e+000,
	4.626837e-002,
	3.852415e+000,
	8.593570e-001,
	-1.021306e+000,
	-1.149551e-001,
	-1.108414e+000,
	4.178343e+000,
	4.013665e-001,
	-2.222814e+000,
	6.929462e-001,
	1.392652e+000,
	4.401662e-001,
	-1.074251e+000,
	-2.224002e-001,
	1.372356e+000,
	-8.858704e+000,
	-3.922660e+000,
	3.020018e+000,
	-1.458724e-002,
	1.511186e+000,
	8.288064e-001,
	-1.062048e+000,
	-1.526582e-001,
	4.921067e-001,
	1.485522e+001,
	-3.229936e+000,
	-8.426604e-001,
	3.916243e-001,
	2.678994e+000,
	6.689264e-001,
	// albedo 0, turbidity 6
	-1.257023e+000,
	-3.364700e-001,
	-1.527795e+001,
	1.504223e+001,
	2.717715e-003,
	3.029910e-001,
	1.636851e-001,
	3.561663e+000,
	5.283161e-001,
	-1.635124e+000,
	-7.329993e-001,
	3.523939e+000,
	-2.566337e+000,
	-1.902543e-002,
	5.505483e-001,
	-6.242176e-002,
	1.065992e+000,
	6.654236e-001,
	-9.295823e-001,
	4.845834e-002,
	-2.992990e-001,
	-2.001327e-001,
	-8.019339e-002,
	1.807806e+000,
	9.020277e-002,
	5.095372e+000,
	8.639936e-001,
	-1.093740e+000,
	-2.148608e-001,
	-5.216240e-001,
	2.119777e+000,
	9.506454e-002,
	-1.831439e+000,
	6.961204e-001,
	1.102084e-001,
	4.384319e-001,
	-1.044181e+000,
	-1.849257e-001,
	9.071246e-001,
	-4.648901e+000,
	-2.279385e+000,
	2.356502e+000,
	-4.169147e-002,
	1.932557e+000,
	8.296550e-001,
	-1.061451e+000,
	-1.458745e-001,
	2.952267e-001,
	8.967214e+000,
	-3.726228e+000,
	-5.022316e-001,
	5.684877e-001,
	3.102347e+000,
	6.658443e-001,
	// albedo 0, turbidity 7
	-1.332391e+000,
	-4.127769e-001,
	-9.328643e+000,
	9.046194e+000,
	3.457775e-003,
	3.377425e-001,
	1.530909e-001,
	3.301209e+000,
	4.997917e-001,
	-1.932002e+000,
	-9.947777e-001,
	-2.042329e+000,
	3.586940e+000,
	-5.642182e-002,
	8.130478e-001,
	-8.195988e-002,
	1.118294e-001,
	5.617231e-001,
	-8.707374e-001,
	1.286999e-001,
	1.820054e+000,
	-4.674706e+000,
	3.317471e-003,
	5.919018e-001,
	1.975278e-001,
	6.686519e+000,
	9.631727e-001,
	-1.070378e+000,
	-3.030579e-001,
	-9.041938e-001,
	6.200201e+000,
	1.232207e-001,
	-3.650628e-001,
	5.029403e-001,
	-2.903162e+000,
	3.811408e-001,
	-1.063035e+000,
	-1.637545e-001,
	5.853072e-001,
	-7.889906e+000,
	-1.200641e+000,
	1.035018e+000,
	1.192093e-001,
	3.267054e+000,
	8.416151e-001,
	-1.053655e+000,
	-1.562286e-001,
	2.423683e-001,
	1.128575e+001,
	-4.363262e+000,
	-7.314160e-002,
	5.642088e-001,
	2.514023e+000,
	6.670457e-001,
	// albedo 0, turbidity 8
	-1.366112e+000,
	-4.718287e-001,
	-7.876222e+000,
	7.746900e+000,
	-9.182309e-003,
	4.716076e-001,
	8.320252e-002,
	3.165603e+000,
	5.392334e-001,
	-2.468204e+000,
	-1.336340e+000,
	-5.386723e+000,
	7.072672e+000,
	-8.329266e-002,
	8.636876e-001,
	-1.978177e-002,
	-1.326218e-001,
	2.979222e-001,
	-9.653522e-001,
	-2.373416e-002,
	1.810250e+000,
	-6.467262e+000,
	1.410706e-001,
	-4.753717e-001,
	3.003095e-001,
	6.551163e+000,
	1.151083e+000,
	-8.943186e-001,
	-2.487152e-001,
	-2.308960e-001,
	8.512648e+000,
	1.298402e-001,
	1.034705e+000,
	2.303509e-001,
	-3.924095e+000,
	2.982717e-001,
	-1.146999e+000,
	-2.318784e-001,
	8.992419e-002,
	-9.933614e+000,
	-8.860920e-001,
	-3.071656e-002,
	2.852012e-001,
	3.046199e+000,
	8.599001e-001,
	-1.032399e+000,
	-1.645145e-001,
	2.683599e-001,
	1.327701e+001,
	-4.407670e+000,
	7.709869e-002,
	4.951727e-001,
	1.957277e+000,
	6.630943e-001,
	// albedo 0, turbidity 9
	-1.469070e+000,
	-6.135092e-001,
	-6.506263e+000,
	6.661315e+000,
	-3.835383e-002,
	7.150413e-001,
	7.784318e-003,
	2.820577e+000,
	6.756784e-001,
	-2.501583e+000,
	-1.247404e+000,
	-1.523462e+001,
	1.633191e+001,
	-1.204803e-002,
	5.896471e-001,
	-2.002023e-002,
	1.144647e+000,
	6.177874e-002,
	-2.438672e+000,
	-1.127291e+000,
	5.731172e+000,
	-1.021350e+001,
	6.165610e-002,
	-7.752641e-001,
	4.708254e-001,
	4.176847e+000,
	1.200881e+000,
	-1.513427e-001,
	9.792731e-002,
	-1.612349e+000,
	9.814289e+000,
	5.188921e-002,
	1.716403e+000,
	-7.039255e-002,
	-2.815115e+000,
	3.291874e-001,
	-1.318511e+000,
	-3.650554e-001,
	4.221268e-001,
	-9.294529e+000,
	-4.397520e-002,
	-8.100625e-001,
	3.742719e-001,
	1.834166e+000,
	8.223450e-001,
	-1.016009e+000,
	-1.820264e-001,
	1.278426e-001,
	1.182696e+001,
	-4.801528e+000,
	4.947899e-001,
	4.660378e-001,
	1.601254e+000,
	6.702359e-001,
	// albedo 0, turbidity 10
	-1.841310e+000,
	-9.781779e-001,
	-4.610903e+000,
	4.824662e+000,
	-5.100806e-002,
	6.463776e-001,
	-6.377724e-006,
	2.216875e+000,
	8.618530e-001,
	-2.376373e+000,
	-1.108657e+000,
	-1.489799e+001,
	1.546458e+001,
	4.091025e-002,
	9.761780e-002,
	-1.048958e-002,
	2.165834e+000,
	-1.609171e-001,
	-4.710318e+000,
	-2.261963e+000,
	6.947327e+000,
	-1.034828e+001,
	-1.325542e-001,
	7.508674e-001,
	2.247553e-001,
	2.873142e+000,
	1.297100e+000,
	2.163750e-001,
	-1.944345e-001,
	-2.437860e+000,
	1.011314e+001,
	4.450500e-001,
	3.111492e-001,
	2.751323e-001,
	-1.627906e+000,
	2.531213e-001,
	-1.258794e+000,
	-3.524641e-001,
	8.425444e-001,
	-1.085313e+001,
	-1.154381e+000,
	-4.638014e-001,
	-2.781115e-003,
	4.344498e-001,
	8.507091e-001,
	-1.018938e+000,
	-1.804153e-001,
	-6.354054e-002,
	1.573150e+001,
	-4.386999e+000,
	6.211115e-001,
	5.294648e-001,
	1.580749e+000,
	6.586655e-001,
	// albedo 1, turbidity 1
	-1.116416e+000,
	-1.917524e-001,
	-1.068233e+001,
	1.222221e+001,
	-3.668978e-002,
	1.054022e+000,
	1.592132e-002,
	3.180583e+000,
	5.627370e-001,
	-1.132341e+000,
	-1.671286e-001,
	5.976499e+000,
	-4.227366e+000,
	-9.542489e-002,
	8.664938e-001,
	8.351793e-003,
	4.876068e+000,
	4.492779e-001,
	-1.087635e+000,
	-3.173679e-001,
	4.314407e-001,
	1.100555e+000,
	-4.410057e-001,
	1.677253e+000,
	-3.005925e-002,
	-4.201249e+000,
	1.070902e+000,
	-1.083031e+000,
	-8.847705e-002,
	1.291773e+000,
	4.546776e-001,
	3.091894e-001,
	7.261760e-001,
	4.203659e-002,
	5.990615e+000,
	3.704756e-001,
	-1.057899e+000,
	-2.246706e-001,
	2.329563e+000,
	-1.219656e+000,
	-5.335260e+000,
	8.545378e-001,
	-3.906209e-002,
	-9.025499e-001,
	7.797348e-001,
	-1.073305e+000,
	-1.522553e-001,
	1.767063e+000,
	1.904280e+000,
	-3.101673e+000,
	3.995856e-001,
	2.905192e-002,
	2.563977e+000,
	5.753067e-001,
	// albedo 1, turbidity 2
	-1.113674e+000,
	-1.759694e-001,
	-9.754125e+000,
	1.087391e+001,
	-3.841093e-002,
	9.524272e-001,
	5.680219e-002,
	4.227034e+000,
	6.029571e-001,
	-1.126496e+000,
	-1.680281e-001,
	5.332352e+000,
	-4.575579e+000,
	-6.761755e-002,
	3.295335e-001,
	1.194896e-001,
	5.570901e+000,
	4.536185e-001,
	-1.103074e+000,
	-2.681801e-001,
	6.571479e-002,
	2.396522e+000,
	-4.551280e-001,
	2.466331e+000,
	-1.232022e-001,
	-3.023201e+000,
	1.086379e+000,
	-1.053299e+000,
	-2.697173e-002,
	8.379121e-001,
	-9.681458e-001,
	5.890692e-001,
	-4.872027e-001,
	2.936929e-001,
	7.510139e+000,
	3.079122e-001,
	-1.079553e+000,
	-2.710448e-001,
	2.462379e+000,
	-3.713554e-001,
	-8.534512e+000,
	1.828242e+000,
	-1.686398e-001,
	-1.961340e+000,
	8.941077e-001,
	-1.069741e+000,
	-1.396394e-001,
	1.657868e+000,
	3.236313e+000,
	-2.706344e+000,
	-2.948122e-001,
	1.314816e-001,
	2.868457e+000,
	5.413403e-001,
	// albedo 1, turbidity 3
	-1.131649e+000,
	-1.954455e-001,
	-7.751595e+000,
	8.685861e+000,
	-4.910871e-002,
	8.992952e-001,
	4.710143e-002,
	4.254818e+000,
	6.821116e-001,
	-1.156689e+000,
	-1.884324e-001,
	3.163519e+000,
	-3.091522e+000,
	-6.613927e-002,
	-2.575883e-002,
	1.640065e-001,
	6.073643e+000,
	4.453468e-001,
	-1.079224e+000,
	-2.621389e-001,
	9.446437e-001,
	1.448479e+000,
	-3.969384e-001,
	2.626638e+000,
	-8.101186e-002,
	-3.016355e+000,
	1.076295e+000,
	-1.080832e+000,
	1.033057e-002,
	-3.500156e-001,
	-3.281419e-002,
	5.655512e-001,
	-1.156742e+000,
	4.534710e-001,
	8.774122e+000,
	2.772869e-001,
	-1.051202e+000,
	-2.679975e-001,
	2.719109e+000,
	-2.190316e+000,
	-6.878798e+000,
	2.250481e+000,
	-2.030252e-001,
	-2.026527e+000,
	9.701096e-001,
	-1.089849e+000,
	-1.598589e-001,
	1.564748e+000,
	6.869187e+000,
	-3.053670e+000,
	-6.110435e-001,
	1.644472e-001,
	2.370452e+000,
	5.511770e-001,
	// albedo 1, turbidity 4
	-1.171419e+000,
	-2.429746e-001,
	-8.991334e+000,
	9.571216e+000,
	-2.772861e-002,
	6.688262e-001,
	7.683478e-002,
	3.785611e+000,
	6.347635e-001,
	-1.228554e+000,
	-2.917562e-001,
	2.753986e+000,
	-2.491780e+000,
	-4.663434e-002,
	3.118303e-001,
	7.546506e-002,
	4.463096e+000,
	5.955071e-001,
	-1.093124e+000,
	-2.447767e-001,
	9.097406e-001,
	5.448296e-001,
	-2.957824e-001,
	2.024167e+000,
	-5.152333e-004,
	-1.069081e+000,
	9.369565e-001,
	-1.056994e+000,
	1.569507e-002,
	-8.217491e-001,
	1.870818e+000,
	7.061930e-001,
	-1.483928e+000,
	5.978206e-001,
	6.864902e+000,
	3.673332e-001,
	-1.054871e+000,
	-2.758129e-001,
	2.712807e+000,
	-5.950110e+000,
	-6.554039e+000,
	2.447523e+000,
	-1.895171e-001,
	-1.454292e+000,
	9.131738e-001,
	-1.100218e+000,
	-1.746241e-001,
	1.438505e+000,
	1.115481e+001,
	-3.266076e+000,
	-8.837357e-001,
	1.970100e-001,
	1.991595e+000,
	5.907821e-001,
	// albedo 1, turbidity 5
	-1.207267e+000,
	-2.913610e-001,
	-1.103767e+001,
	1.140724e+001,
	-1.416800e-002,
	5.564047e-001,
	8.476262e-002,
	3.371255e+000,
	6.221335e-001,
	-1.429698e+000,
	-5.374218e-001,
	2.837524e+000,
	-2.221936e+000,
	-2.422337e-002,
	9.313758e-002,
	7.190250e-002,
	1.869022e+000,
	5.609035e-001,
	-1.002274e+000,
	-6.972810e-002,
	4.031308e-001,
	-3.932997e-001,
	-1.521923e-001,
	2.390646e+000,
	-6.893990e-002,
	2.999661e+000,
	1.017843e+000,
	-1.081168e+000,
	-1.178666e-001,
	-4.968080e-001,
	3.919299e+000,
	6.046866e-001,
	-2.440615e+000,
	7.891538e-001,
	2.140835e+000,
	2.740470e-001,
	-1.050727e+000,
	-2.307688e-001,
	2.276396e+000,
	-9.454407e+000,
	-5.505176e+000,
	2.992620e+000,
	-2.450942e-001,
	6.078372e-001,
	9.606765e-001,
	-1.103752e+000,
	-1.810202e-001,
	1.375044e+000,
	1.589095e+001,
	-3.438954e+000,
	-1.265669e+000,
	2.475172e-001,
	1.680768e+000,
	5.978056e-001,
	// albedo 1, turbidity 6
	-1.244324e+000,
	-3.378542e-001,
	-1.111001e+001,
	1.137784e+001,
	-7.896794e-003,
	4.808023e-001,
	9.249904e-002,
	3.025816e+000,
	5.880239e-001,
	-1.593165e+000,
	-7.027621e-001,
	2.220896e+000,
	-1.437709e+000,
	-1.534738e-002,
	6.286958e-002,
	6.644555e-002,
	1.091727e+000,
	5.470080e-001,
	-9.136506e-001,
	1.344874e-002,
	7.772636e-001,
	-1.209396e+000,
	-1.408978e-001,
	2.433718e+000,
	-1.041938e-001,
	3.791244e+000,
	1.037916e+000,
	-1.134968e+000,
	-1.803315e-001,
	-9.267335e-001,
	4.576670e+000,
	6.851928e-001,
	-2.805000e+000,
	8.687208e-001,
	1.161483e+000,
	2.571688e-001,
	-1.017037e+000,
	-2.053943e-001,
	2.361640e+000,
	-9.887818e+000,
	-5.122889e+000,
	3.287088e+000,
	-2.594102e-001,
	8.578927e-001,
	9.592340e-001,
	-1.118723e+000,
	-1.934942e-001,
	1.226023e+000,
	1.674140e+001,
	-3.277335e+000,
	-1.629809e+000,
	2.765232e-001,
	1.637713e+000,
	6.113963e-001,
	// albedo 1, turbidity 7
	-1.314779e+000,
	-4.119915e-001,
	-1.241150e+001,
	1.241578e+001,
	2.344284e-003,
	2.980837e-001,
	1.414613e-001,
	2.781731e+000,
	4.998556e-001,
	-1.926199e+000,
	-1.020038e+000,
	2.569200e+000,
	-1.081159e+000,
	-2.266833e-002,
	3.588668e-001,
	8.750078e-003,
	-2.452171e-001,
	4.796758e-001,
	-7.780002e-001,
	1.850647e-001,
	4.445456e-002,
	-2.409297e+000,
	-7.816346e-002,
	1.546790e+000,
	-2.807227e-002,
	5.998176e+000,
	1.132396e+000,
	-1.179326e+000,
	-3.578330e-001,
	-2.392933e-001,
	6.467883e+000,
	5.904596e-001,
	-1.869975e+000,
	8.045839e-001,
	-2.498121e+000,
	1.610633e-001,
	-1.009956e+000,
	-1.311896e-001,
	1.726577e+000,
	-1.219356e+001,
	-3.466239e+000,
	2.343602e+000,
	-2.252205e-001,
	2.573681e+000,
	1.027109e+000,
	-1.112460e+000,
	-2.063093e-001,
	1.233051e+000,
	2.058946e+001,
	-4.578074e+000,
	-1.145643e+000,
	3.160192e-001,
	1.420159e+000,
	5.860212e-001,
	// albedo 1, turbidity 8
	-1.371689e+000,
	-4.914196e-001,
	-1.076610e+001,
	1.107405e+001,
	-1.485077e-002,
	5.936218e-001,
	3.685482e-002,
	2.599968e+000,
	6.002204e-001,
	-2.436997e+000,
	-1.377939e+000,
	2.130141e-002,
	1.079593e+000,
	-1.796232e-002,
	-3.933248e-002,
	1.610711e-001,
	-6.901181e-001,
	1.206416e-001,
	-8.743368e-001,
	7.331370e-002,
	8.734259e-001,
	-3.743126e+000,
	-3.151167e-002,
	1.297596e+000,
	-7.634926e-002,
	6.532873e+000,
	1.435737e+000,
	-9.810197e-001,
	-3.521634e-001,
	-2.855205e-001,
	7.134674e+000,
	6.839748e-001,
	-1.394841e+000,
	6.952036e-001,
	-4.633104e+000,
	-2.173401e-002,
	-1.122958e+000,
	-1.691536e-001,
	1.382360e+000,
	-1.102913e+001,
	-2.608171e+000,
	1.865111e+000,
	-1.345154e-001,
	3.112342e+000,
	1.094134e+000,
	-1.075586e+000,
	-2.077415e-001,
	1.171477e+000,
	1.793270e+001,
	-4.656858e+000,
	-1.036839e+000,
	3.338295e-001,
	1.042793e+000,
	5.739374e-001,
	// albedo 1, turbidity 9
	-1.465871e+000,
	-6.364486e-001,
	-8.833718e+000,
	9.343650e+000,
	-3.223600e-002,
	7.552848e-001,
	-3.121341e-006,
	2.249164e+000,
	8.094662e-001,
	-2.448924e+000,
	-1.270878e+000,
	-4.823703e+000,
	5.853058e+000,
	-2.149127e-002,
	3.581132e-002,
	-1.230276e-003,
	4.892553e-001,
	-1.597657e-001,
	-2.419809e+000,
	-1.071337e+000,
	1.575648e+000,
	-4.983580e+000,
	9.545185e-003,
	5.032615e-001,
	4.186266e-001,
	4.634147e+000,
	1.433517e+000,
	-1.383278e-001,
	-2.797095e-002,
	-1.943067e-001,
	6.679623e+000,
	4.118280e-001,
	-2.744289e-001,
	-2.118722e-002,
	-4.337025e+000,
	1.505072e-001,
	-1.341872e+000,
	-2.518572e-001,
	1.027009e+000,
	-6.527103e+000,
	-1.081271e+000,
	1.015465e+000,
	2.845789e-001,
	2.470371e+000,
	9.278120e-001,
	-1.040640e+000,
	-2.367454e-001,
	1.100744e+000,
	8.827253e+000,
	-4.560794e+000,
	-7.287017e-001,
	2.842503e-001,
	6.336593e-001,
	6.327335e-001,
	// albedo 1, turbidity 10
	-1.877993e+000,
	-1.025135e+000,
	-4.311037e+000,
	4.715016e+000,
	-4.711631e-002,
	6.335844e-001,
	-7.665398e-006,
	1.788017e+000,
	9.001409e-001,
	-2.281540e+000,
	-1.137668e+000,
	-1.036869e+001,
	1.136254e+001,
	1.961739e-002,
	-9.836174e-002,
	-6.734567e-003,
	1.320918e+000,
	-2.400807e-001,
	-4.904054e+000,
	-2.315781e+000,
	5.735999e+000,
	-8.626257e+000,
	-1.255643e-001,
	1.545446e+000,
	1.396860e-001,
	2.972897e+000,
	1.429934e+000,
	4.077067e-001,
	-1.833688e-001,
	-2.450939e+000,
	9.119433e+000,
	4.505361e-001,
	-1.340828e+000,
	3.973690e-001,
	-1.785370e+000,
	9.628711e-002,
	-1.296052e+000,
	-3.250526e-001,
	1.813294e+000,
	-1.031485e+001,
	-1.388690e+000,
	1.239733e+000,
	-8.989196e-002,
	-3.389637e-001,
	9.639560e-001,
	-1.062181e+000,
	-2.423444e-001,
	7.577592e-001,
	1.566938e+001,
	-4.462264e+000,
	-5.742810e-001,
	3.262259e-001,
	9.461672e-001,
	6.232887e-001,
};

double[] datasetXYZRad1 = new double[]
{
	// albedo 0, turbidity 1
	1.560219e+000,
	1.417388e+000,
	1.206927e+000,
	1.091949e+001,
	5.931416e+000,
	7.304788e+000,
	// albedo 0, turbidity 2
	1.533049e+000,
	1.560532e+000,
	3.685059e-001,
	1.355040e+001,
	5.543711e+000,
	7.792189e+000,
	// albedo 0, turbidity 3
	1.471043e+000,
	1.746088e+000,
	-9.299697e-001,
	1.720362e+001,
	5.473384e+000,
	8.336416e+000,
	// albedo 0, turbidity 4
	1.355991e+000,
	2.109348e+000,
	-3.295855e+000,
	2.264843e+001,
	5.454607e+000,
	9.304656e+000,
	// albedo 0, turbidity 5
	1.244963e+000,
	2.547533e+000,
	-5.841485e+000,
	2.756879e+001,
	5.576104e+000,
	1.043287e+001,
	// albedo 0, turbidity 6
	1.175532e+000,
	2.784634e+000,
	-7.212225e+000,
	2.975347e+001,
	6.472980e+000,
	1.092331e+001,
	// albedo 0, turbidity 7
	1.082973e+000,
	3.118094e+000,
	-8.934293e+000,
	3.186879e+001,
	8.473885e+000,
	1.174019e+001,
	// albedo 0, turbidity 8
	9.692500e-001,
	3.349574e+000,
	-1.003810e+001,
	3.147654e+001,
	1.338931e+001,
	1.272547e+001,
	// albedo 0, turbidity 9
	8.547044e-001,
	3.151538e+000,
	-9.095567e+000,
	2.554995e+001,
	2.273219e+001,
	1.410398e+001,
	// albedo 0, turbidity 10
	7.580340e-001,
	2.311153e+000,
	-5.170814e+000,
	1.229669e+001,
	3.686529e+001,
	1.598882e+001,
	// albedo 1, turbidity 1
	1.664273e+000,
	1.574468e+000,
	1.422078e+000,
	9.768247e+000,
	1.447338e+001,
	1.644988e+001,
	// albedo 1, turbidity 2
	1.638295e+000,
	1.719586e+000,
	5.786675e-001,
	1.239846e+001,
	1.415419e+001,
	1.728605e+001,
	// albedo 1, turbidity 3
	1.572623e+000,
	1.921559e+000,
	-7.714802e-001,
	1.609246e+001,
	1.420954e+001,
	1.825908e+001,
	// albedo 1, turbidity 4
	1.468395e+000,
	2.211970e+000,
	-2.845869e+000,
	2.075027e+001,
	1.524822e+001,
	1.937622e+001,
	// albedo 1, turbidity 5
	1.355047e+000,
	2.556469e+000,
	-4.960920e+000,
	2.460237e+001,
	1.648360e+001,
	2.065648e+001,
	// albedo 1, turbidity 6
	1.291642e+000,
	2.742036e+000,
	-6.061967e+000,
	2.602002e+001,
	1.819144e+001,
	2.116712e+001,
	// albedo 1, turbidity 7
	1.194565e+000,
	2.972120e+000,
	-7.295779e+000,
	2.691805e+001,
	2.124880e+001,
	2.201819e+001,
	// albedo 1, turbidity 8
	1.083631e+000,
	3.047021e+000,
	-7.766096e+000,
	2.496261e+001,
	2.744264e+001,
	2.291875e+001,
	// albedo 1, turbidity 9
	9.707994e-001,
	2.736459e+000,
	-6.308284e+000,
	1.760860e+001,
	3.776291e+001,
	2.392150e+001,
	// albedo 1, turbidity 10
	8.574294e-001,
	1.865155e+000,
	-2.364707e+000,
	4.337793e+000,
	5.092831e+001,
	2.523432e+001,
};

double[] datasetXYZ2 = new double[]
{
	// albedo 0, turbidity 1
	-1.127942e+000,
	-1.905548e-001,
	-1.252356e+001,
	1.375799e+001,
	-3.624732e-002,
	1.055453e+000,
	1.385036e-002,
	4.176970e+000,
	5.928345e-001,
	-1.155260e+000,
	-1.778135e-001,
	6.216056e+000,
	-5.254116e+000,
	-8.787445e-002,
	8.434621e-001,
	4.025734e-002,
	6.195322e+000,
	3.111856e-001,
	-1.125624e+000,
	-3.217593e-001,
	5.043919e-001,
	1.686284e+000,
	-3.536071e-001,
	1.476321e+000,
	-7.899019e-002,
	-4.522531e+000,
	1.271691e+000,
	-1.081801e+000,
	-1.033234e-001,
	9.995550e-001,
	7.482946e-003,
	-6.776018e-002,
	1.463141e+000,
	9.492021e-002,
	5.612723e+000,
	1.298846e-001,
	-1.075320e+000,
	-2.402711e-001,
	2.141284e+000,
	-1.203359e+000,
	-4.945188e+000,
	1.437221e+000,
	-8.096750e-002,
	-1.028378e+000,
	1.004164e+000,
	-1.073337e+000,
	-1.516517e-001,
	1.639379e+000,
	2.304669e+000,
	-3.214244e+000,
	1.286245e+000,
	5.613957e-002,
	2.480902e+000,
	4.999363e-001,
	// albedo 0, turbidity 2
	-1.128399e+000,
	-1.857793e-001,
	-1.089863e+001,
	1.172984e+001,
	-3.768099e-002,
	9.439285e-001,
	4.869335e-002,
	4.845114e+000,
	6.119211e-001,
	-1.114002e+000,
	-1.399280e-001,
	4.963800e+000,
	-4.685500e+000,
	-7.780879e-002,
	4.049736e-001,
	1.586297e-001,
	7.770264e+000,
	3.449006e-001,
	-1.185472e+000,
	-3.403543e-001,
	6.588322e-001,
	1.133713e+000,
	-4.118674e-001,
	2.061191e+000,
	-1.882768e-001,
	-4.372586e+000,
	1.223530e+000,
	-1.002272e+000,
	2.000703e-002,
	7.073269e-002,
	1.485075e+000,
	5.005589e-001,
	4.301494e-001,
	3.626541e-001,
	7.921098e+000,
	1.574766e-001,
	-1.121006e+000,
	-3.007777e-001,
	2.242051e+000,
	-4.571561e+000,
	-7.761071e+000,
	2.053404e+000,
	-1.524018e-001,
	-1.886162e+000,
	1.018208e+000,
	-1.058864e+000,
	-1.358673e-001,
	1.389667e+000,
	8.633409e+000,
	-3.437249e+000,
	7.295429e-001,
	1.514700e-001,
	2.842513e+000,
	5.014325e-001,
	// albedo 0, turbidity 3
	-1.144464e+000,
	-2.043799e-001,
	-1.020188e+001,
	1.071247e+001,
	-3.256693e-002,
	7.860205e-001,
	6.872719e-002,
	4.824771e+000,
	6.259836e-001,
	-1.170104e+000,
	-2.118626e-001,
	4.391405e+000,
	-4.198900e+000,
	-7.111559e-002,
	3.890442e-001,
	1.024831e-001,
	6.282535e+000,
	5.365688e-001,
	-1.129171e+000,
	-2.552880e-001,
	2.238298e-001,
	7.314295e-001,
	-3.562730e-001,
	1.881931e+000,
	-3.078716e-002,
	-1.039120e+000,
	9.096301e-001,
	-1.042294e+000,
	4.450203e-003,
	-5.116033e-001,
	2.627589e+000,
	6.098996e-001,
	-1.264638e-001,
	4.325281e-001,
	7.080503e+000,
	4.583646e-001,
	-1.082293e+000,
	-2.723056e-001,
	2.065076e+000,
	-8.143133e+000,
	-7.892212e+000,
	2.142231e+000,
	-7.106240e-002,
	-1.122398e+000,
	8.338505e-001,
	-1.071715e+000,
	-1.426568e-001,
	1.095351e+000,
	1.729783e+001,
	-3.851931e+000,
	4.360514e-001,
	2.114440e-001,
	2.970832e+000,
	5.944389e-001,
	// albedo 0, turbidity 4
	-1.195909e+000,
	-2.590449e-001,
	-1.191037e+001,
	1.207947e+001,
	-1.589842e-002,
	6.297846e-001,
	9.054772e-002,
	4.285959e+000,
	5.933752e-001,
	-1.245763e+000,
	-3.316637e-001,
	4.293660e+000,
	-3.694011e+000,
	-4.699947e-002,
	4.843684e-001,
	2.130425e-002,
	4.097549e+000,
	6.530809e-001,
	-1.148742e+000,
	-1.902509e-001,
	-2.393233e-001,
	-2.441254e-001,
	-2.610918e-001,
	1.846988e+000,
	3.532866e-002,
	2.660106e+000,
	8.358294e-001,
	-1.016080e+000,
	-7.444960e-002,
	-5.053436e-001,
	4.388855e+000,
	6.054987e-001,
	-1.208300e+000,
	5.817215e-001,
	2.543570e+000,
	4.726568e-001,
	-1.072027e+000,
	-2.101440e-001,
	1.518378e+000,
	-1.060119e+001,
	-6.016546e+000,
	2.649475e+000,
	-5.166992e-002,
	1.571269e+000,
	8.344622e-001,
	-1.072365e+000,
	-1.511201e-001,
	7.478010e-001,
	1.900732e+001,
	-3.950387e+000,
	-3.473907e-001,
	3.797211e-001,
	2.782949e+000,
	6.296808e-001,
	// albedo 0, turbidity 5
	-1.239423e+000,
	-3.136289e-001,
	-1.351100e+001,
	1.349468e+001,
	-7.070423e-003,
	5.012315e-001,
	1.106008e-001,
	3.803619e+000,
	5.577948e-001,
	-1.452524e+000,
	-5.676944e-001,
	2.993153e+000,
	-2.277288e+000,
	-2.168954e-002,
	3.056720e-001,
	1.152338e-002,
	1.852697e+000,
	6.427228e-001,
	-1.061421e+000,
	-4.590521e-002,
	6.057022e-001,
	-1.096835e+000,
	-1.504952e-001,
	2.344921e+000,
	-5.491832e-002,
	5.268322e+000,
	9.082253e-001,
	-1.042373e+000,
	-1.769498e-001,
	-1.075388e+000,
	3.831712e+000,
	3.154140e-001,
	-2.416458e+000,
	7.909032e-001,
	-1.492892e-002,
	3.854049e-001,
	-1.064159e+000,
	-1.892684e-001,
	1.438685e+000,
	-8.166362e+000,
	-3.616364e+000,
	3.275206e+000,
	-1.203825e-001,
	2.039491e+000,
	8.688057e-001,
	-1.070120e+000,
	-1.569508e-001,
	4.124760e-001,
	1.399683e+001,
	-3.547085e+000,
	-1.046326e+000,
	4.973825e-001,
	2.791231e+000,
	6.503286e-001,
	// albedo 0, turbidity 6
	-1.283579e+000,
	-3.609518e-001,
	-1.335397e+001,
	1.315248e+001,
	-4.431938e-004,
	3.769526e-001,
	1.429824e-001,
	3.573613e+000,
	4.998696e-001,
	-1.657952e+000,
	-7.627948e-001,
	1.958222e+000,
	-7.949816e-001,
	-2.882837e-002,
	5.356149e-001,
	-5.191946e-002,
	8.869955e-001,
	6.263320e-001,
	-9.527600e-001,
	6.494189e-002,
	5.361303e-001,
	-2.129590e+000,
	-9.258630e-002,
	1.604776e+000,
	5.067770e-002,
	6.376055e+000,
	9.138052e-001,
	-1.080827e+000,
	-2.523120e-001,
	-7.154262e-001,
	4.120085e+000,
	1.878228e-001,
	-1.492158e+000,
	6.881655e-001,
	-1.446611e+000,
	4.040631e-001,
	-1.054075e+000,
	-1.665498e-001,
	9.191052e-001,
	-6.636943e+000,
	-1.894826e+000,
	2.107810e+000,
	-3.680499e-002,
	2.655452e+000,
	8.413840e-001,
	-1.061127e+000,
	-1.448849e-001,
	2.667493e-001,
	1.034103e+001,
	-4.285769e+000,
	-3.874504e-001,
	5.998752e-001,
	3.132426e+000,
	6.652753e-001,
	// albedo 0, turbidity 7
	-1.347345e+000,
	-4.287832e-001,
	-9.305553e+000,
	9.133813e+000,
	-3.173527e-003,
	3.977564e-001,
	1.151420e-001,
	3.320564e+000,
	4.998134e-001,
	-1.927296e+000,
	-9.901372e-001,
	-2.593499e+000,
	4.087421e+000,
	-5.833993e-002,
	8.158929e-001,
	-4.681279e-002,
	2.423716e-001,
	4.938052e-001,
	-9.470092e-001,
	7.325237e-002,
	2.064735e+000,
	-5.167540e+000,
	-1.313751e-002,
	4.832169e-001,
	1.126295e-001,
	6.970522e+000,
	1.035022e+000,
	-1.022557e+000,
	-2.762616e-001,
	-9.375748e-001,
	6.696739e+000,
	2.200765e-001,
	-1.133253e-001,
	5.492505e-001,
	-3.109391e+000,
	3.321914e-001,
	-1.087444e+000,
	-1.836263e-001,
	6.225024e-001,
	-8.576765e+000,
	-1.107637e+000,
	7.859427e-001,
	9.910909e-002,
	3.112938e+000,
	8.596261e-001,
	-1.051544e+000,
	-1.546262e-001,
	2.371731e-001,
	1.200502e+001,
	-4.527291e+000,
	7.268862e-002,
	5.571478e-001,
	2.532873e+000,
	6.662000e-001,
	// albedo 0, turbidity 8
	-1.375576e+000,
	-4.840019e-001,
	-8.121290e+000,
	8.058140e+000,
	-1.445661e-002,
	5.123314e-001,
	5.813321e-002,
	3.203219e+000,
	5.442318e-001,
	-2.325221e+000,
	-1.241463e+000,
	-7.063430e+000,
	8.741369e+000,
	-7.829950e-002,
	8.844273e-001,
	-3.471106e-002,
	1.740583e-001,
	2.814079e-001,
	-1.228700e+000,
	-2.013412e-001,
	2.949042e+000,
	-7.371945e+000,
	1.071753e-001,
	-2.491970e-001,
	2.265223e-001,
	6.391504e+000,
	1.172389e+000,
	-7.601786e-001,
	-1.680631e-001,
	-7.584444e-001,
	8.541356e+000,
	8.222291e-002,
	6.729633e-001,
	3.206615e-001,
	-3.700940e+000,
	2.710054e-001,
	-1.191166e+000,
	-2.672347e-001,
	2.927498e-001,
	-9.713613e+000,
	-4.783721e-001,
	2.352803e-001,
	2.161949e-001,
	2.691481e+000,
	8.745447e-001,
	-1.030135e+000,
	-1.653301e-001,
	2.263443e-001,
	1.296157e+001,
	-4.650644e+000,
	7.055709e-003,
	5.091975e-001,
	2.000370e+000,
	6.603839e-001,
	// albedo 0, turbidity 9
	-1.508018e+000,
	-6.460933e-001,
	-6.402745e+000,
	6.545995e+000,
	-3.750320e-002,
	6.921803e-001,
	3.309819e-003,
	2.797527e+000,
	6.978446e-001,
	-2.333308e+000,
	-1.167837e+000,
	-1.746787e+001,
	1.868630e+001,
	-8.948229e-003,
	5.621946e-001,
	-3.402626e-002,
	1.217943e+000,
	1.149865e-002,
	-2.665953e+000,
	-1.226307e+000,
	7.169725e+000,
	-1.159434e+001,
	3.583420e-002,
	-3.074378e-001,
	3.412248e-001,
	4.422122e+000,
	1.283791e+000,
	-9.705116e-002,
	8.312991e-002,
	-2.160462e+000,
	1.028235e+001,
	3.543357e-002,
	1.032049e+000,
	1.058310e-001,
	-2.972898e+000,
	2.418628e-001,
	-1.329617e+000,
	-3.699557e-001,
	5.560117e-001,
	-9.730113e+000,
	9.938865e-002,
	-3.071488e-001,
	2.510691e-001,
	1.777111e+000,
	8.705142e-001,
	-1.019387e+000,
	-1.893247e-001,
	1.194079e-001,
	1.239436e+001,
	-4.799224e+000,
	2.940213e-001,
	4.841268e-001,
	1.529724e+000,
	6.582615e-001,
	// albedo 0, turbidity 10
	-1.896737e+000,
	-1.005442e+000,
	-6.411032e+000,
	6.548220e+000,
	-3.227596e-002,
	5.717262e-001,
	-8.115192e-006,
	2.296704e+000,
	9.000749e-001,
	-2.411116e+000,
	-1.225587e+000,
	-1.753629e+001,
	1.829393e+001,
	1.247555e-002,
	2.364616e-001,
	-5.114637e-003,
	1.603778e+000,
	-2.224156e-001,
	-4.707121e+000,
	-2.074977e+000,
	7.942300e+000,
	-1.132407e+001,
	-5.415654e-002,
	5.446811e-001,
	1.032493e-001,
	4.010235e+000,
	1.369802e+000,
	1.010482e-001,
	-4.013305e-001,
	-2.674579e+000,
	9.779409e+000,
	1.782506e-001,
	7.053045e-001,
	4.200002e-001,
	-2.400671e+000,
	1.953165e-001,
	-1.243526e+000,
	-3.391255e-001,
	8.848882e-001,
	-9.789025e+000,
	-3.997324e-001,
	-9.546227e-001,
	-1.044017e-001,
	6.010593e-001,
	8.714462e-001,
	-1.014633e+000,
	-1.730009e-001,
	-7.738934e-002,
	1.390903e+001,
	-4.847307e+000,
	1.076059e+000,
	5.685743e-001,
	1.572992e+000,
	6.561432e-001,
	// albedo 1, turbidity 1
	-1.122998e+000,
	-1.881183e-001,
	-1.030709e+001,
	1.158932e+001,
	-4.079495e-002,
	9.603774e-001,
	3.079436e-002,
	4.009235e+000,
	5.060745e-001,
	-1.134790e+000,
	-1.539688e-001,
	5.478405e+000,
	-4.217270e+000,
	-1.043858e-001,
	7.165008e-001,
	1.524765e-002,
	6.473623e+000,
	4.207882e-001,
	-1.134957e+000,
	-3.513318e-001,
	7.393837e-001,
	1.354415e+000,
	-4.764078e-001,
	1.690441e+000,
	-5.492640e-002,
	-5.563523e+000,
	1.145743e+000,
	-1.058344e+000,
	-5.758503e-002,
	1.168230e+000,
	3.269824e-001,
	1.795193e-001,
	7.849011e-001,
	7.441853e-002,
	6.904804e+000,
	2.818790e-001,
	-1.075194e+000,
	-2.355813e-001,
	2.463685e+000,
	-1.536505e+000,
	-7.505771e+000,
	9.619712e-001,
	-6.465851e-002,
	-1.355492e+000,
	8.489847e-001,
	-1.079030e+000,
	-1.465328e-001,
	1.773838e+000,
	2.310131e+000,
	-3.136065e+000,
	3.507952e-001,
	4.435014e-002,
	2.819225e+000,
	5.689008e-001,
	// albedo 1, turbidity 2
	-1.125833e+000,
	-1.870849e-001,
	-9.555833e+000,
	1.059713e+001,
	-4.225402e-002,
	9.164663e-001,
	4.338796e-002,
	4.400980e+000,
	6.056119e-001,
	-1.127440e+000,
	-1.551891e-001,
	4.755621e+000,
	-4.408806e+000,
	-7.851763e-002,
	2.268284e-001,
	1.460070e-001,
	7.048003e+000,
	3.525997e-001,
	-1.143788e+000,
	-3.170178e-001,
	5.480669e-001,
	2.041830e+000,
	-4.532139e-001,
	2.302233e+000,
	-1.887419e-001,
	-4.489221e+000,
	1.250967e+000,
	-1.032849e+000,
	7.376031e-003,
	5.666073e-001,
	-2.312203e-001,
	4.862894e-001,
	-1.748294e-001,
	3.572870e-001,
	8.380522e+000,
	1.302333e-001,
	-1.093728e+000,
	-2.786977e-001,
	2.641272e+000,
	-1.507494e+000,
	-8.731243e+000,
	1.684055e+000,
	-2.023377e-001,
	-2.176398e+000,
	1.013249e+000,
	-1.076578e+000,
	-1.456205e-001,
	1.693935e+000,
	2.945003e+000,
	-2.822673e+000,
	-2.520033e-001,
	1.517034e-001,
	2.649109e+000,
	5.179094e-001,
	// albedo 1, turbidity 3
	-1.146417e+000,
	-2.119353e-001,
	-7.187525e+000,
	8.058599e+000,
	-5.256438e-002,
	8.375733e-001,
	3.887093e-002,
	4.222111e+000,
	6.695347e-001,
	-1.173674e+000,
	-2.067025e-001,
	2.899359e+000,
	-2.804918e+000,
	-8.473899e-002,
	3.944225e-003,
	1.340641e-001,
	6.160887e+000,
	4.527141e-001,
	-1.090098e+000,
	-2.599633e-001,
	9.180856e-001,
	1.092710e+000,
	-4.215019e-001,
	2.427660e+000,
	-9.277667e-002,
	-2.123523e+000,
	1.058159e+000,
	-1.084460e+000,
	8.056181e-003,
	-2.453510e-001,
	6.619567e-001,
	4.668118e-001,
	-9.526719e-001,
	4.648454e-001,
	8.001572e+000,
	3.054194e-001,
	-1.053728e+000,
	-2.765784e-001,
	2.792388e+000,
	-3.489517e+000,
	-8.150535e+000,
	2.195757e+000,
	-2.017234e-001,
	-2.128017e+000,
	9.326589e-001,
	-1.099348e+000,
	-1.593939e-001,
	1.568292e+000,
	7.247853e+000,
	-2.933000e+000,
	-5.890481e-001,
	1.724440e-001,
	2.433484e+000,
	5.736558e-001,
	// albedo 1, turbidity 4
	-1.185983e+000,
	-2.581184e-001,
	-7.761056e+000,
	8.317053e+000,
	-3.351773e-002,
	6.676667e-001,
	5.941733e-002,
	3.820727e+000,
	6.324032e-001,
	-1.268591e+000,
	-3.398067e-001,
	2.348503e+000,
	-2.023779e+000,
	-5.368458e-002,
	1.083282e-001,
	8.402858e-002,
	3.910254e+000,
	5.577481e-001,
	-1.071353e+000,
	-1.992459e-001,
	7.878387e-001,
	1.974702e-001,
	-3.033058e-001,
	2.335298e+000,
	-8.205259e-002,
	7.954454e-001,
	9.972312e-001,
	-1.089513e+000,
	-3.104364e-002,
	-5.995746e-001,
	2.330281e+000,
	6.581939e-001,
	-1.821467e+000,
	6.679973e-001,
	5.090195e+000,
	3.125161e-001,
	-1.040214e+000,
	-2.570934e-001,
	2.660489e+000,
	-6.506045e+000,
	-7.053586e+000,
	2.763153e+000,
	-2.433632e-001,
	-7.648176e-001,
	9.452937e-001,
	-1.116052e+000,
	-1.831993e-001,
	1.457694e+000,
	1.163608e+001,
	-3.216426e+000,
	-1.045594e+000,
	2.285002e-001,
	1.817407e+000,
	5.810396e-001,
	// albedo 1, turbidity 5
	-1.230134e+000,
	-3.136264e-001,
	-8.909301e+000,
	9.145006e+000,
	-1.055387e-002,
	4.467317e-001,
	1.016826e-001,
	3.342964e+000,
	5.633840e-001,
	-1.442907e+000,
	-5.593147e-001,
	2.156447e+000,
	-1.241657e+000,
	-3.512130e-002,
	3.050274e-001,
	1.797175e-002,
	1.742358e+000,
	5.977153e-001,
	-1.027627e+000,
	-6.481539e-002,
	4.351975e-001,
	-1.051677e+000,
	-2.030672e-001,
	1.942684e+000,
	-3.615993e-002,
	4.050266e+000,
	9.801624e-001,
	-1.082110e+000,
	-1.578209e-001,
	-3.397511e-001,
	4.163851e+000,
	6.650368e-001,
	-1.841730e+000,
	7.062544e-001,
	6.789881e-001,
	3.172623e-001,
	-1.047447e+000,
	-1.977560e-001,
	2.183364e+000,
	-8.805249e+000,
	-5.483962e+000,
	2.551309e+000,
	-1.779640e-001,
	1.519501e+000,
	9.212536e-001,
	-1.111853e+000,
	-1.935736e-001,
	1.394408e+000,
	1.392405e+001,
	-3.465430e+000,
	-1.068432e+000,
	2.388671e-001,
	1.455336e+000,
	6.233425e-001,
	// albedo 1, turbidity 6
	-1.262238e+000,
	-3.546341e-001,
	-1.008703e+001,
	1.020084e+001,
	-1.852187e-003,
	3.537580e-001,
	1.239199e-001,
	3.056093e+000,
	5.132052e-001,
	-1.613810e+000,
	-7.355585e-001,
	2.760123e+000,
	-1.685253e+000,
	-2.517552e-002,
	2.914258e-001,
	4.743448e-003,
	8.689596e-001,
	5.674192e-001,
	-9.462336e-001,
	2.950767e-002,
	-2.613816e-001,
	-7.398653e-001,
	-1.315558e-001,
	1.901042e+000,
	-6.447844e-002,
	4.969341e+000,
	1.027342e+000,
	-1.111481e+000,
	-2.194054e-001,
	-9.004538e-002,
	3.983442e+000,
	4.871278e-001,
	-1.965315e+000,
	7.956121e-001,
	-2.363225e-001,
	2.718037e-001,
	-1.036397e+000,
	-1.827106e-001,
	1.964747e+000,
	-8.870759e+000,
	-4.208011e+000,
	2.461215e+000,
	-2.158905e-001,
	1.561676e+000,
	9.436866e-001,
	-1.113769e+000,
	-1.947819e-001,
	1.300720e+000,
	1.516476e+001,
	-4.088732e+000,
	-1.069384e+000,
	2.836434e-001,
	1.671451e+000,
	6.229612e-001,
	// albedo 1, turbidity 7
	-1.328069e+000,
	-4.244047e-001,
	-8.417040e+000,
	8.552244e+000,
	-6.813504e-003,
	4.127422e-001,
	9.619897e-002,
	2.854227e+000,
	5.059880e-001,
	-1.927552e+000,
	-1.025290e+000,
	9.529576e-001,
	4.255950e-001,
	-3.738779e-002,
	2.584586e-001,
	4.911004e-002,
	-2.640913e-001,
	4.138626e-001,
	-8.488094e-001,
	1.435988e-001,
	6.356807e-001,
	-2.895732e+000,
	-8.473961e-002,
	1.701305e+000,
	-1.323908e-001,
	6.499338e+000,
	1.210928e+000,
	-1.128313e+000,
	-3.397048e-001,
	-4.043140e-001,
	6.265097e+000,
	5.482395e-001,
	-2.057614e+000,
	8.884087e-001,
	-2.943879e+000,
	9.760301e-002,
	-1.039764e+000,
	-1.494772e-001,
	1.781915e+000,
	-1.153012e+001,
	-3.379232e+000,
	2.517231e+000,
	-2.764393e-001,
	2.588849e+000,
	1.052120e+000,
	-1.108447e+000,
	-2.012251e-001,
	1.198640e+000,
	1.925331e+001,
	-4.423892e+000,
	-1.257122e+000,
	3.395690e-001,
	1.481220e+000,
	5.880175e-001,
	// albedo 1, turbidity 8
	-1.374185e+000,
	-4.967434e-001,
	-7.401318e+000,
	7.724021e+000,
	-2.345723e-002,
	5.979653e-001,
	2.436346e-002,
	2.658970e+000,
	6.014891e-001,
	-2.310933e+000,
	-1.290290e+000,
	-1.301909e+000,
	2.557806e+000,
	-3.744449e-002,
	8.982861e-002,
	1.090613e-001,
	-4.398363e-001,
	1.184329e-001,
	-1.124730e+000,
	-9.921830e-002,
	1.366902e+000,
	-4.172489e+000,
	-5.078016e-002,
	1.393597e+000,
	-9.323843e-002,
	6.452721e+000,
	1.435913e+000,
	-8.468477e-001,
	-2.744819e-001,
	-4.347200e-001,
	6.713362e+000,
	6.127133e-001,
	-1.685634e+000,
	7.360941e-001,
	-4.535502e+000,
	-2.920866e-002,
	-1.165242e+000,
	-2.008697e-001,
	1.438778e+000,
	-1.008936e+001,
	-2.214771e+000,
	2.102909e+000,
	-1.763085e-001,
	2.859075e+000,
	1.093470e+000,
	-1.074614e+000,
	-2.066374e-001,
	1.131891e+000,
	1.630063e+001,
	-4.801441e+000,
	-1.112590e+000,
	3.595785e-001,
	1.122227e+000,
	5.794610e-001,
	// albedo 1, turbidity 9
	-1.521515e+000,
	-6.835604e-001,
	-5.571044e+000,
	6.028774e+000,
	-4.253715e-002,
	6.875746e-001,
	-5.279456e-006,
	2.180150e+000,
	8.487705e-001,
	-2.240415e+000,
	-1.171166e+000,
	-7.182771e+000,
	8.417068e+000,
	-1.932866e-002,
	1.101887e-001,
	-1.098862e-002,
	6.242195e-001,
	-2.393875e-001,
	-2.712354e+000,
	-1.198830e+000,
	3.180200e+000,
	-6.768130e+000,
	-2.563386e-003,
	7.984607e-001,
	2.764376e-001,
	4.695358e+000,
	1.557045e+000,
	-3.655172e-002,
	-2.142321e-002,
	-9.138120e-001,
	7.932786e+000,
	3.516542e-001,
	-7.994343e-001,
	1.786761e-001,
	-4.208399e+000,
	1.820576e-002,
	-1.368610e+000,
	-2.656212e-001,
	1.249397e+000,
	-8.317818e+000,
	-8.962772e-001,
	1.423249e+000,
	1.478381e-001,
	2.191660e+000,
	1.007748e+000,
	-1.041753e+000,
	-2.453366e-001,
	1.061102e+000,
	1.130172e+001,
	-4.739312e+000,
	-9.223334e-001,
	2.982776e-001,
	6.162931e-001,
	6.080302e-001,
	// albedo 1, turbidity 10
	-1.989159e+000,
	-1.095160e+000,
	-2.915550e+000,
	3.275339e+000,
	-5.735765e-002,
	5.742174e-001,
	-7.683288e-006,
	1.763400e+000,
	9.001342e-001,
	-2.070020e+000,
	-1.086338e+000,
	-1.095898e+001,
	1.206960e+001,
	3.780123e-002,
	-1.774699e-002,
	-5.881348e-004,
	1.333819e+000,
	-2.605423e-001,
	-5.249653e+000,
	-2.383040e+000,
	6.160406e+000,
	-9.097138e+000,
	-1.955319e-001,
	1.651785e+000,
	6.016463e-004,
	3.021824e+000,
	1.493574e+000,
	4.685432e-001,
	-2.358662e-001,
	-2.666433e+000,
	9.685763e+000,
	5.804928e-001,
	-1.521875e+000,
	5.668989e-001,
	-1.548136e+000,
	1.688642e-002,
	-1.296891e+000,
	-3.449031e-001,
	1.928548e+000,
	-1.167560e+001,
	-1.627615e+000,
	1.355603e+000,
	-1.929074e-001,
	-6.568952e-001,
	1.009774e+000,
	-1.067288e+000,
	-2.410392e-001,
	7.147961e-001,
	1.783840e+001,
	-4.374399e+000,
	-6.588777e-001,
	3.329831e-001,
	1.012066e+000,
	6.118645e-001,
};

double[] datasetXYZRad2 = new double[]
{
	// albedo 0, turbidity 1
	1.632341e+000,
	1.395230e+000,
	1.375634e+000,
	1.238193e+001,
	5.921102e+000,
	7.766508e+000,
	// albedo 0, turbidity 2
	1.597115e+000,
	1.554617e+000,
	3.932382e-001,
	1.505284e+001,
	5.725234e+000,
	8.158155e+000,
	// albedo 0, turbidity 3
	1.522034e+000,
	1.844545e+000,
	-1.322862e+000,
	1.918382e+001,
	5.440769e+000,
	8.837119e+000,
	// albedo 0, turbidity 4
	1.403048e+000,
	2.290852e+000,
	-4.013792e+000,
	2.485100e+001,
	5.521888e+000,
	9.845547e+000,
	// albedo 0, turbidity 5
	1.286364e+000,
	2.774498e+000,
	-6.648221e+000,
	2.964151e+001,
	5.923777e+000,
	1.097075e+001,
	// albedo 0, turbidity 6
	1.213544e+000,
	3.040195e+000,
	-8.092676e+000,
	3.186082e+001,
	6.789782e+000,
	1.158899e+001,
	// albedo 0, turbidity 7
	1.122622e+000,
	3.347465e+000,
	-9.649016e+000,
	3.343824e+001,
	9.347715e+000,
	1.231374e+001,
	// albedo 0, turbidity 8
	1.007356e+000,
	3.543858e+000,
	-1.053520e+001,
	3.239842e+001,
	1.483962e+001,
	1.331718e+001,
	// albedo 0, turbidity 9
	8.956642e-001,
	3.278700e+000,
	-9.254933e+000,
	2.557923e+001,
	2.489677e+001,
	1.476166e+001,
	// albedo 0, turbidity 10
	7.985143e-001,
	2.340404e+000,
	-4.928274e+000,
	1.141787e+001,
	3.961501e+001,
	1.682448e+001,
	// albedo 1, turbidity 1
	1.745162e+000,
	1.639467e+000,
	1.342721e+000,
	1.166033e+001,
	1.490124e+001,
	1.774031e+001,
	// albedo 1, turbidity 2
	1.708439e+000,
	1.819144e+000,
	2.834399e-001,
	1.448066e+001,
	1.459214e+001,
	1.858679e+001,
	// albedo 1, turbidity 3
	1.631720e+000,
	2.094799e+000,
	-1.378825e+000,
	1.843198e+001,
	1.463173e+001,
	1.962881e+001,
	// albedo 1, turbidity 4
	1.516536e+000,
	2.438729e+000,
	-3.624121e+000,
	2.298621e+001,
	1.599782e+001,
	2.070027e+001,
	// albedo 1, turbidity 5
	1.405863e+000,
	2.785191e+000,
	-5.705236e+000,
	2.645121e+001,
	1.768330e+001,
	2.191903e+001,
	// albedo 1, turbidity 6
	1.344052e+000,
	2.951807e+000,
	-6.683851e+000,
	2.744271e+001,
	1.985706e+001,
	2.229452e+001,
	// albedo 1, turbidity 7
	1.245827e+000,
	3.182923e+000,
	-7.822960e+000,
	2.791395e+001,
	2.327254e+001,
	2.315910e+001,
	// albedo 1, turbidity 8
	1.132305e+000,
	3.202593e+000,
	-8.008429e+000,
	2.521093e+001,
	3.000014e+001,
	2.405306e+001,
	// albedo 1, turbidity 9
	1.020330e+000,
	2.820556e+000,
	-6.238704e+000,
	1.709276e+001,
	4.077916e+001,
	2.509949e+001,
	// albedo 1, turbidity 10
	9.031570e-001,
	1.863917e+000,
	-1.955738e+000,
	3.032665e+000,
	5.434290e+001,
	2.641780e+001,
};

double[] datasetXYZ3 = new double[]
{
	// albedo 0, turbidity 1
	-1.310023e+000,
	-4.407658e-001,
	-3.640340e+001,
	3.683292e+001,
	-8.124762e-003,
	5.297961e-001,
	1.188633e-002,
	3.138320e+000,
	5.134778e-001,
	-1.424100e+000,
	-5.501606e-001,
	-1.753510e+001,
	1.822769e+001,
	-1.539272e-002,
	6.366826e-001,
	2.661996e-003,
	2.659915e+000,
	4.071138e-001,
	-1.103436e+000,
	-1.884105e-001,
	6.425322e+000,
	-6.910579e+000,
	-2.019861e-002,
	3.553271e-001,
	-1.589061e-002,
	5.345985e+000,
	8.790218e-001,
	-1.186200e+000,
	-4.307514e-001,
	-3.957947e+000,
	5.979352e+000,
	-5.348869e-002,
	1.736117e+000,
	3.491346e-002,
	-2.692261e+000,
	5.610506e-001,
	-1.006038e+000,
	-1.305995e-001,
	4.473513e+000,
	-3.806719e+000,
	1.419407e-001,
	-2.148238e-002,
	-5.081185e-002,
	3.735362e+000,
	5.358280e-001,
	-1.078507e+000,
	-1.633754e-001,
	-3.812368e+000,
	4.381700e+000,
	2.988122e-002,
	1.754224e+000,
	1.472376e-001,
	3.722798e+000,
	4.999157e-001,
	// albedo 0, turbidity 2
	-1.333582e+000,
	-4.649908e-001,
	-3.359528e+001,
	3.404375e+001,
	-9.384242e-003,
	5.587511e-001,
	5.726310e-003,
	3.073145e+000,
	5.425529e-001,
	-1.562624e+000,
	-7.107068e-001,
	-1.478170e+001,
	1.559839e+001,
	-1.462375e-002,
	5.050133e-001,
	2.516017e-002,
	1.604696e+000,
	2.902403e-001,
	-8.930158e-001,
	4.068077e-002,
	1.373481e+000,
	-2.342752e+000,
	-2.098058e-002,
	6.248686e-001,
	-5.258363e-002,
	7.058214e+000,
	1.150373e+000,
	-1.262823e+000,
	-4.818353e-001,
	8.892610e-004,
	1.923120e+000,
	-4.979718e-002,
	1.040693e+000,
	1.558103e-001,
	-2.852480e+000,
	2.420691e-001,
	-9.968383e-001,
	-1.200648e-001,
	1.324342e+000,
	-9.430889e-001,
	1.931098e-001,
	4.436916e-001,
	-7.320456e-002,
	4.215931e+000,
	7.898019e-001,
	-1.078185e+000,
	-1.718192e-001,
	-1.720191e+000,
	2.358918e+000,
	2.765637e-002,
	1.260245e+000,
	2.021941e-001,
	3.395483e+000,
	5.173628e-001,
	// albedo 0, turbidity 3
	-1.353023e+000,
	-4.813523e-001,
	-3.104920e+001,
	3.140156e+001,
	-9.510741e-003,
	5.542030e-001,
	8.135471e-003,
	3.136646e+000,
	5.215989e-001,
	-1.624704e+000,
	-7.990201e-001,
	-2.167125e+001,
	2.246341e+001,
	-1.163533e-002,
	5.415746e-001,
	2.618378e-002,
	1.139214e+000,
	3.444357e-001,
	-7.983610e-001,
	1.417476e-001,
	9.914841e+000,
	-1.081503e+001,
	-1.218845e-002,
	3.411392e-001,
	-6.137698e-002,
	7.445848e+000,
	1.180080e+000,
	-1.266679e+000,
	-4.288977e-001,
	-5.818701e+000,
	6.986437e+000,
	-8.180711e-002,
	1.397403e+000,
	2.016916e-001,
	-1.275731e+000,
	2.592773e-001,
	-1.009707e+000,
	-1.537754e-001,
	3.496378e+000,
	-3.013726e+000,
	2.421150e-001,
	-2.831925e-001,
	3.003395e-002,
	3.702862e+000,
	7.746320e-001,
	-1.075646e+000,
	-1.768747e-001,
	-1.347762e+000,
	1.989004e+000,
	1.375836e-002,
	1.764810e+000,
	1.330018e-001,
	3.230864e+000,
	6.626210e-001,
	// albedo 0, turbidity 4
	-1.375269e+000,
	-5.103569e-001,
	-3.442661e+001,
	3.478703e+001,
	-8.460009e-003,
	5.408643e-001,
	4.813323e-003,
	3.016078e+000,
	5.062069e-001,
	-1.821679e+000,
	-9.766461e-001,
	-1.926488e+001,
	1.997912e+001,
	-9.822567e-003,
	3.649556e-001,
	4.316092e-002,
	8.930190e-001,
	4.166527e-001,
	-6.633542e-001,
	1.997841e-001,
	2.395592e+000,
	-3.117175e+000,
	-1.080884e-002,
	8.983814e-001,
	-1.375825e-001,
	6.673463e+000,
	1.115663e+000,
	-1.303240e+000,
	-3.612712e-001,
	8.292959e-002,
	3.381364e-001,
	-6.078648e-002,
	3.229247e-001,
	3.680987e-001,
	7.046755e-001,
	3.144924e-001,
	-9.952598e-001,
	-2.039076e-001,
	4.026851e-001,
	2.686684e-001,
	1.640712e-001,
	5.186341e-001,
	-1.205520e-002,
	2.659613e+000,
	8.030394e-001,
	-1.098579e+000,
	-2.151992e-001,
	6.558198e-001,
	-7.436900e-004,
	-1.421817e-003,
	1.073701e+000,
	1.886875e-001,
	2.536857e+000,
	6.673923e-001,
	// albedo 0, turbidity 5
	-1.457986e+000,
	-5.906842e-001,
	-3.812464e+001,
	3.838539e+001,
	-6.024357e-003,
	4.741484e-001,
	1.209223e-002,
	2.818432e+000,
	5.012433e-001,
	-1.835728e+000,
	-1.003405e+000,
	-6.848129e+000,
	7.601943e+000,
	-1.277375e-002,
	4.785598e-001,
	3.366853e-002,
	1.097701e+000,
	4.636635e-001,
	-8.491348e-001,
	9.466365e-003,
	-2.685226e+000,
	2.004060e+000,
	-1.168708e-002,
	6.752316e-001,
	-1.543371e-001,
	5.674759e+000,
	1.039534e+000,
	-1.083379e+000,
	-1.506790e-001,
	7.328236e-001,
	-5.095568e-001,
	-8.609153e-002,
	4.448820e-001,
	4.174662e-001,
	1.481556e+000,
	3.942551e-001,
	-1.117089e+000,
	-3.337605e-001,
	2.502281e-001,
	4.036323e-001,
	2.673899e-001,
	2.829817e-001,
	2.242450e-002,
	2.043207e+000,
	7.706902e-001,
	-1.071648e+000,
	-2.126200e-001,
	6.069466e-001,
	-1.456290e-003,
	-5.515960e-001,
	1.046755e+000,
	1.985021e-001,
	2.290245e+000,
	6.876058e-001,
	// albedo 0, turbidity 6
	-1.483903e+000,
	-6.309647e-001,
	-4.380213e+001,
	4.410537e+001,
	-5.712161e-003,
	5.195992e-001,
	2.028428e-003,
	2.687114e+000,
	5.098321e-001,
	-2.053976e+000,
	-1.141473e+000,
	5.109183e-001,
	8.060391e-002,
	-1.033983e-002,
	4.066532e-001,
	4.869627e-002,
	1.161722e+000,
	4.039525e-001,
	-6.348185e-001,
	7.651292e-002,
	-1.031327e+001,
	1.007598e+001,
	-2.083688e-002,
	7.359516e-001,
	-2.029459e-001,
	5.013257e+000,
	1.077649e+000,
	-1.228630e+000,
	-1.650496e-001,
	4.077157e-002,
	-7.189167e-001,
	-5.092220e-002,
	2.959814e-001,
	5.111496e-001,
	2.540433e+000,
	3.615330e-001,
	-1.041883e+000,
	-3.278413e-001,
	-6.691911e-002,
	1.307364e+000,
	2.166663e-001,
	3.000595e-001,
	-3.157136e-003,
	1.389208e+000,
	7.999026e-001,
	-1.103556e+000,
	-2.443602e-001,
	4.705347e-001,
	-9.296482e-004,
	-5.309920e-001,
	9.654511e-001,
	2.142587e-001,
	2.244723e+000,
	6.839976e-001,
	// albedo 0, turbidity 7
	-1.555684e+000,
	-6.962113e-001,
	-4.647983e+001,
	4.674270e+001,
	-5.034895e-003,
	4.755090e-001,
	-9.502561e-007,
	2.626569e+000,
	5.056194e-001,
	-1.998288e+000,
	-1.124720e+000,
	-1.629586e+000,
	2.187993e+000,
	-8.284384e-003,
	3.845258e-001,
	5.726240e-002,
	1.185644e+000,
	4.255812e-001,
	-1.032570e+000,
	-2.513850e-001,
	-3.721112e+000,
	3.506967e+000,
	-2.186561e-002,
	9.436049e-001,
	-2.451412e-001,
	4.725724e+000,
	1.039256e+000,
	-8.597532e-001,
	9.073332e-002,
	-2.553741e+000,
	1.993237e+000,
	-4.390891e-002,
	-2.046928e-001,
	5.515623e-001,
	1.909127e+000,
	3.948212e-001,
	-1.210482e+000,
	-4.477622e-001,
	-2.267805e-001,
	1.219488e+000,
	1.336186e-001,
	6.866897e-001,
	2.808997e-002,
	1.600403e+000,
	7.816409e-001,
	-1.078168e+000,
	-2.699261e-001,
	2.537282e-001,
	3.820684e-001,
	-4.425103e-001,
	5.298235e-001,
	2.185217e-001,
	1.728679e+000,
	6.882743e-001,
	// albedo 0, turbidity 8
	-1.697968e+000,
	-8.391488e-001,
	-5.790105e+001,
	5.814120e+001,
	-3.404760e-003,
	4.265140e-001,
	-1.796301e-006,
	2.368442e+000,
	5.324429e-001,
	-2.141552e+000,
	-1.172230e+000,
	1.677872e+001,
	-1.641470e+001,
	-5.732425e-003,
	2.002199e-001,
	6.841834e-002,
	1.485338e+000,
	3.215763e-001,
	-1.442946e+000,
	-7.264245e-001,
	-9.503706e+000,
	9.650462e+000,
	-2.120995e-002,
	1.419263e+000,
	-2.893098e-001,
	3.860731e+000,
	1.120857e+000,
	-5.696752e-001,
	3.411279e-001,
	-2.931035e-001,
	-6.512552e-001,
	-1.068437e-001,
	-1.085661e+000,
	6.107549e-001,
	1.459503e+000,
	3.210336e-001,
	-1.313839e+000,
	-5.921371e-001,
	-2.332222e-001,
	1.648196e+000,
	2.492787e-001,
	1.381033e+000,
	-1.993392e-002,
	9.812560e-001,
	8.316329e-001,
	-1.087464e+000,
	-3.195534e-001,
	2.902095e-001,
	3.383709e-001,
	-8.798482e-001,
	1.494668e-002,
	2.529703e-001,
	1.452644e+000,
	6.693870e-001,
	// albedo 0, turbidity 9
	-2.068582e+000,
	-1.118605e+000,
	-5.081598e+001,
	5.097486e+001,
	-3.280669e-003,
	4.067371e-001,
	-2.544951e-006,
	2.179497e+000,
	5.778017e-001,
	-1.744693e+000,
	-8.537207e-001,
	2.234361e+001,
	-2.208318e+001,
	-5.932616e-003,
	1.035049e-001,
	5.742772e-002,
	1.977880e+000,
	2.124846e-001,
	-3.287515e+000,
	-2.140268e+000,
	-1.249566e+001,
	1.240091e+001,
	-2.409349e-002,
	1.397821e+000,
	-2.371627e-001,
	2.771192e+000,
	1.170496e+000,
	5.502311e-001,
	1.046630e+000,
	2.193517e+000,
	-2.220400e+000,
	-1.064394e-001,
	-1.017926e+000,
	4.795457e-001,
	1.030644e+000,
	3.177516e-001,
	-1.719734e+000,
	-9.536198e-001,
	-6.586821e-001,
	1.386361e+000,
	-2.513065e-002,
	1.187011e+000,
	6.542539e-002,
	5.296055e-001,
	8.082660e-001,
	-1.005700e+000,
	-3.028096e-001,
	4.470957e-002,
	1.007760e+000,
	-8.119016e-001,
	3.153338e-002,
	2.311321e-001,
	1.182208e+000,
	6.824758e-001,
	// albedo 0, turbidity 10
	-2.728867e+000,
	-1.580388e+000,
	-3.079627e+001,
	3.092586e+001,
	-4.197673e-003,
	3.154759e-001,
	-3.897675e-006,
	1.920567e+000,
	6.664791e-001,
	-1.322495e+000,
	-7.249275e-001,
	1.477660e+001,
	-1.468154e+001,
	-9.044857e-003,
	5.624314e-002,
	6.498392e-002,
	2.047389e+000,
	6.367540e-002,
	-6.102376e+000,
	-3.473018e+000,
	-9.926071e+000,
	9.637797e+000,
	-1.097909e-002,
	1.103498e+000,
	-2.424521e-001,
	2.520748e+000,
	1.240260e+000,
	1.351796e+000,
	1.018588e+000,
	2.009081e+000,
	-1.333394e+000,
	-1.979125e-001,
	-3.318292e-001,
	4.476624e-001,
	9.095235e-001,
	2.955611e-001,
	-1.774467e+000,
	-1.079880e+000,
	-8.084680e-002,
	2.577697e-001,
	-1.149295e-001,
	4.975303e-001,
	2.931611e-003,
	-3.803171e-001,
	8.002794e-001,
	-9.898401e-001,
	-2.542513e-001,
	-7.530911e-002,
	1.870355e+000,
	-1.521918e+000,
	2.405164e-001,
	2.964615e-001,
	1.334800e+000,
	6.789053e-001,
	// albedo 1, turbidity 1
	-1.279730e+000,
	-4.290674e-001,
	-4.277972e+001,
	4.343305e+001,
	-6.541826e-003,
	4.945086e-001,
	1.425338e-002,
	2.685244e+000,
	5.011313e-001,
	-1.449506e+000,
	-5.766374e-001,
	-1.688496e+001,
	1.781118e+001,
	-1.121649e-002,
	3.545020e-001,
	2.287338e-002,
	1.904281e+000,
	4.936998e-001,
	-1.021980e+000,
	-1.897574e-001,
	2.482462e+000,
	-2.941725e+000,
	-1.570448e-002,
	7.532578e-001,
	-4.256800e-002,
	5.239660e+000,
	4.983116e-001,
	-1.162608e+000,
	-3.428049e-001,
	3.974358e+000,
	-1.527935e+000,
	-3.919201e-002,
	8.758593e-001,
	7.291363e-002,
	-3.455257e+000,
	8.007426e-001,
	-9.929985e-001,
	-8.712006e-002,
	-7.397313e-001,
	1.348372e+000,
	9.511685e-002,
	3.233584e-001,
	-7.549148e-002,
	5.806452e+000,
	4.990042e-001,
	-1.084996e+000,
	-1.739767e-001,
	1.580475e-001,
	9.088180e-001,
	6.871433e-002,
	5.933079e-001,
	1.188921e-001,
	3.074079e+000,
	4.999327e-001,
	// albedo 1, turbidity 2
	-1.317009e+000,
	-4.661946e-001,
	-4.255347e+001,
	4.312782e+001,
	-5.727235e-003,
	4.285447e-001,
	2.189854e-002,
	2.608310e+000,
	5.190700e-001,
	-1.469236e+000,
	-6.282139e-001,
	-1.241404e+001,
	1.348765e+001,
	-1.204770e-002,
	5.070285e-001,
	-7.280216e-004,
	1.491533e+000,
	3.635064e-001,
	-9.713808e-001,
	-8.138038e-002,
	3.709854e-001,
	-1.041174e+000,
	-1.814075e-002,
	5.060860e-001,
	-2.053756e-002,
	6.161431e+000,
	1.093736e+000,
	-1.159057e+000,
	-3.698074e-001,
	2.711209e+000,
	-6.006479e-001,
	-4.896926e-002,
	9.273957e-001,
	1.137712e-001,
	-3.496828e+000,
	2.867109e-001,
	-1.011601e+000,
	-8.201890e-002,
	2.105725e-001,
	4.597520e-001,
	1.478925e-001,
	2.138940e-001,
	-5.660670e-002,
	6.057755e+000,
	7.859121e-001,
	-1.078020e+000,
	-1.811580e-001,
	1.646622e-001,
	8.348426e-001,
	1.149064e-001,
	4.985738e-001,
	1.376605e-001,
	2.746607e+000,
	4.999626e-001,
	// albedo 1, turbidity 3
	-1.325672e+000,
	-4.769313e-001,
	-4.111215e+001,
	4.168293e+001,
	-6.274997e-003,
	4.649469e-001,
	1.119411e-002,
	2.631267e+000,
	5.234546e-001,
	-1.619391e+000,
	-8.000253e-001,
	-1.534098e+001,
	1.632706e+001,
	-1.012023e-002,
	4.242255e-001,
	2.931597e-002,
	8.925807e-001,
	3.314765e-001,
	-7.356979e-001,
	1.368406e-001,
	2.972579e+000,
	-3.535359e+000,
	-1.318948e-002,
	4.607620e-001,
	-7.182778e-002,
	6.254100e+000,
	1.236299e+000,
	-1.316217e+000,
	-4.194427e-001,
	3.489902e-002,
	1.289849e+000,
	-4.755960e-002,
	1.138222e+000,
	1.975992e-001,
	-8.991542e-001,
	2.290572e-001,
	-9.502188e-001,
	-1.172703e-001,
	1.405202e+000,
	-3.061919e-001,
	1.058772e-001,
	-3.760592e-001,
	-1.983179e-002,
	3.562353e+000,
	7.895959e-001,
	-1.100117e+000,
	-1.900567e-001,
	4.925030e-001,
	5.250225e-001,
	1.576804e-001,
	1.042701e+000,
	7.330743e-002,
	2.796064e+000,
	6.749783e-001,
	// albedo 1, turbidity 4
	-1.354183e+000,
	-5.130625e-001,
	-4.219268e+001,
	4.271772e+001,
	-5.365373e-003,
	4.136743e-001,
	1.235172e-002,
	2.520122e+000,
	5.187269e-001,
	-1.741434e+000,
	-9.589761e-001,
	-8.230339e+000,
	9.296799e+000,
	-9.600162e-003,
	4.994969e-001,
	2.955452e-002,
	3.667099e-001,
	3.526999e-001,
	-6.917347e-001,
	2.154887e-001,
	-8.760264e-001,
	2.334121e-001,
	-1.909621e-002,
	4.748033e-001,
	-1.138514e-001,
	6.515360e+000,
	1.225097e+000,
	-1.293189e+000,
	-4.218700e-001,
	1.620952e+000,
	-7.858597e-001,
	-3.769410e-002,
	6.636786e-001,
	3.364945e-001,
	-5.341017e-001,
	2.128347e-001,
	-9.735521e-001,
	-1.325495e-001,
	1.007517e+000,
	2.598258e-001,
	6.762169e-002,
	1.421018e-003,
	-6.915987e-002,
	3.185897e+000,
	8.641956e-001,
	-1.094800e+000,
	-1.962062e-001,
	5.755591e-001,
	2.906259e-001,
	2.625748e-001,
	7.644049e-001,
	1.347492e-001,
	2.677126e+000,
	6.465460e-001,
	// albedo 1, turbidity 5
	-1.393063e+000,
	-5.578338e-001,
	-4.185249e+001,
	4.233504e+001,
	-5.435640e-003,
	4.743765e-001,
	7.422477e-003,
	2.442801e+000,
	5.211707e-001,
	-1.939487e+000,
	-1.128509e+000,
	-8.974257e+000,
	9.978383e+000,
	-7.965597e-003,
	2.948830e-001,
	4.436763e-002,
	2.839868e-001,
	3.440424e-001,
	-6.011562e-001,
	2.354877e-001,
	-3.079820e+000,
	2.585094e+000,
	-2.002701e-002,
	7.793909e-001,
	-1.598414e-001,
	5.834678e+000,
	1.202856e+000,
	-1.315676e+000,
	-3.903446e-001,
	1.701900e+000,
	-1.304609e+000,
	-1.045121e-002,
	2.747707e-001,
	4.143967e-001,
	3.197102e-001,
	2.637580e-001,
	-9.618628e-001,
	-1.625841e-001,
	1.187138e+000,
	1.497802e-001,
	-5.590954e-006,
	3.178475e-002,
	-4.153145e-002,
	2.496096e+000,
	8.195082e-001,
	-1.111554e+000,
	-2.365546e-001,
	7.831875e-001,
	2.018684e-001,
	2.074369e-001,
	7.395978e-001,
	1.225730e-001,
	1.876478e+000,
	6.821167e-001,
	// albedo 1, turbidity 6
	-1.427879e+000,
	-5.994879e-001,
	-3.531016e+001,
	3.581581e+001,
	-6.431497e-003,
	4.554192e-001,
	7.348731e-004,
	2.334619e+000,
	5.233377e-001,
	-1.998177e+000,
	-1.206633e+000,
	-2.146510e+001,
	2.242237e+001,
	-5.857596e-003,
	2.755663e-001,
	6.384795e-002,
	1.358244e-001,
	3.328437e-001,
	-6.440630e-001,
	2.058571e-001,
	2.155499e+000,
	-2.587968e+000,
	-1.840023e-002,
	8.826555e-001,
	-2.222452e-001,
	5.847073e+000,
	1.228387e+000,
	-1.229071e+000,
	-3.360441e-001,
	-3.429599e-001,
	6.179469e-001,
	2.029610e-003,
	8.899319e-002,
	5.041624e-001,
	1.882964e-001,
	2.252040e-001,
	-1.022905e+000,
	-2.101621e-001,
	1.915689e+000,
	-6.498794e-001,
	-3.463651e-002,
	8.954605e-002,
	-6.797854e-002,
	2.417705e+000,
	8.568618e-001,
	-1.082538e+000,
	-2.007723e-001,
	4.731009e-001,
	4.077267e-001,
	1.324289e-001,
	6.514880e-001,
	1.702912e-001,
	2.309383e+000,
	6.600895e-001,
	// albedo 1, turbidity 7
	-1.472139e+000,
	-6.499815e-001,
	-3.428465e+001,
	3.469659e+001,
	-5.747023e-003,
	4.174167e-001,
	1.688597e-003,
	2.323046e+000,
	5.395191e-001,
	-2.161176e+000,
	-1.353089e+000,
	-2.226827e+001,
	2.329138e+001,
	-5.583808e-003,
	2.364793e-001,
	6.096656e-002,
	1.944666e-003,
	2.861624e-001,
	-6.593044e-001,
	1.393558e-001,
	4.698373e+000,
	-5.193883e+000,
	-1.998390e-002,
	1.095635e+000,
	-2.391254e-001,
	5.598103e+000,
	1.236193e+000,
	-1.195717e+000,
	-2.972715e-001,
	4.648953e-002,
	3.024588e-001,
	5.003313e-003,
	-3.754741e-001,
	5.247265e-001,
	-1.381312e-001,
	2.493896e-001,
	-1.020139e+000,
	-2.253524e-001,
	3.548437e-001,
	7.030485e-001,
	-2.107076e-002,
	4.581395e-001,
	-3.243757e-002,
	2.453259e+000,
	8.323623e-001,
	-1.098770e+000,
	-2.435780e-001,
	8.761614e-001,
	1.941613e-001,
	-1.990692e-001,
	3.761139e-001,
	1.657412e-001,
	1.590503e+000,
	6.741417e-001,
	// albedo 1, turbidity 8
	-1.648007e+000,
	-8.205121e-001,
	-4.435106e+001,
	4.479801e+001,
	-4.181353e-003,
	3.854830e-001,
	-1.842385e-006,
	2.000281e+000,
	5.518363e-001,
	-2.140986e+000,
	-1.282239e+000,
	-3.979213e+000,
	4.672459e+000,
	-5.008582e-003,
	2.421920e-001,
	6.253602e-002,
	6.612713e-001,
	2.555851e-001,
	-1.300502e+000,
	-5.137898e-001,
	5.179821e-001,
	-4.032341e-001,
	-2.066785e-002,
	1.087929e+000,
	-2.615309e-001,
	4.225887e+000,
	1.229237e+000,
	-6.963340e-001,
	9.241060e-002,
	6.936356e-002,
	-3.588571e-001,
	-5.461843e-002,
	-5.616643e-001,
	5.484166e-001,
	-4.776267e-002,
	2.414935e-001,
	-1.233179e+000,
	-4.325498e-001,
	6.479813e-001,
	8.368356e-001,
	2.458875e-001,
	6.464752e-001,
	-2.897097e-002,
	1.561773e+000,
	8.518598e-001,
	-1.051023e+000,
	-2.533690e-001,
	1.004294e+000,
	3.028083e-001,
	-1.520108e+000,
	1.607013e-001,
	1.619975e-001,
	1.131094e+000,
	6.706655e-001,
	// albedo 1, turbidity 9
	-1.948249e+000,
	-1.097383e+000,
	-4.453697e+001,
	4.494902e+001,
	-3.579939e-003,
	3.491605e-001,
	-2.500253e-006,
	1.740442e+000,
	6.188022e-001,
	-2.154253e+000,
	-1.209559e+000,
	4.144894e+000,
	-3.562411e+000,
	-5.638843e-003,
	1.067169e-001,
	7.594858e-002,
	1.005280e+000,
	1.072543e-001,
	-2.513259e+000,
	-1.507208e+000,
	-1.602979e+000,
	1.404154e+000,
	-5.560750e-003,
	1.240490e+000,
	-2.852117e-001,
	3.485252e+000,
	1.349321e+000,
	-7.832214e-002,
	3.655626e-001,
	3.856288e-001,
	6.867894e-001,
	-1.609523e-001,
	-6.704306e-001,
	5.357301e-001,
	-6.457935e-001,
	1.479503e-001,
	-1.354784e+000,
	-5.454375e-001,
	8.797469e-001,
	-1.466514e+000,
	7.134420e-001,
	5.934903e-001,
	-2.911178e-002,
	8.643737e-001,
	9.030724e-001,
	-1.048324e+000,
	-2.738736e-001,
	8.783074e-001,
	3.246188e+000,
	-4.435369e+000,
	1.251791e-001,
	1.783486e-001,
	1.064657e+000,
	6.522878e-001,
	// albedo 1, turbidity 10
	-2.770408e+000,
	-1.618911e+000,
	-2.504031e+001,
	2.531674e+001,
	-4.239279e-003,
	3.241013e-001,
	-3.764484e-006,
	1.586843e+000,
	7.035906e-001,
	-1.913500e+000,
	-1.144014e+000,
	-1.080587e+001,
	1.153677e+001,
	-1.003197e-002,
	1.577515e-001,
	5.217789e-002,
	1.225278e+000,
	5.172771e-003,
	-5.293208e+000,
	-2.876463e+000,
	2.087053e+000,
	-3.201552e+000,
	3.892964e-003,
	5.323930e-001,
	-2.034512e-001,
	2.617760e+000,
	1.273597e+000,
	9.060340e-001,
	3.773409e-001,
	-6.399945e-001,
	3.213979e+000,
	-9.112172e-002,
	6.494055e-001,
	3.953280e-001,
	5.047796e-001,
	2.998695e-001,
	-1.482179e+000,
	-6.778310e-001,
	1.161775e+000,
	-3.004872e+000,
	4.774797e-001,
	-4.969248e-001,
	-3.512074e-003,
	-1.307190e+000,
	7.927378e-001,
	-9.863181e-001,
	-1.803364e-001,
	5.810824e-001,
	4.580570e+000,
	-3.863454e+000,
	5.328174e-001,
	2.272821e-001,
	1.771114e+000,
	6.791814e-001,
};

double[] datasetXYZRad3 = new double[]
{
	// albedo 0, turbidity 1
	1.168084e+000,
	2.156455e+000,
	-3.980314e+000,
	1.989302e+001,
	1.328335e+001,
	1.435621e+001,
	// albedo 0, turbidity 2
	1.135488e+000,
	2.294701e+000,
	-4.585886e+000,
	2.090208e+001,
	1.347840e+001,
	1.467658e+001,
	// albedo 0, turbidity 3
	1.107408e+000,
	2.382765e+000,
	-5.112357e+000,
	2.147823e+001,
	1.493128e+001,
	1.460882e+001,
	// albedo 0, turbidity 4
	1.054193e+000,
	2.592891e+000,
	-6.115000e+000,
	2.268967e+001,
	1.635672e+001,
	1.518999e+001,
	// albedo 0, turbidity 5
	1.006946e+000,
	2.705420e+000,
	-6.698930e+000,
	2.291830e+001,
	1.834324e+001,
	1.570651e+001,
	// albedo 0, turbidity 6
	9.794044e-001,
	2.742440e+000,
	-6.805283e+000,
	2.225271e+001,
	2.050797e+001,
	1.563130e+001,
	// albedo 0, turbidity 7
	9.413577e-001,
	2.722009e+000,
	-6.760707e+000,
	2.098242e+001,
	2.342588e+001,
	1.605011e+001,
	// albedo 0, turbidity 8
	8.917923e-001,
	2.592780e+000,
	-6.152635e+000,
	1.774141e+001,
	2.858324e+001,
	1.657910e+001,
	// albedo 0, turbidity 9
	8.288391e-001,
	2.153434e+000,
	-4.118327e+000,
	1.078118e+001,
	3.681710e+001,
	1.738139e+001,
	// albedo 0, turbidity 10
	7.623528e-001,
	1.418187e+000,
	-8.845235e-001,
	7.590129e-001,
	4.629859e+001,
	1.921657e+001,
	// albedo 1, turbidity 1
	1.352858e+000,
	2.048862e+000,
	-2.053393e+000,
	1.405874e+001,
	3.045344e+001,
	3.044430e+001,
	// albedo 1, turbidity 2
	1.330497e+000,
	2.126497e+000,
	-2.466296e+000,
	1.467559e+001,
	3.090738e+001,
	3.069707e+001,
	// albedo 1, turbidity 3
	1.286344e+000,
	2.200436e+000,
	-2.877228e+000,
	1.492701e+001,
	3.236288e+001,
	3.077223e+001,
	// albedo 1, turbidity 4
	1.234428e+000,
	2.289628e+000,
	-3.404699e+000,
	1.499436e+001,
	3.468390e+001,
	3.084842e+001,
	// albedo 1, turbidity 5
	1.178660e+000,
	2.306071e+000,
	-3.549159e+000,
	1.411006e+001,
	3.754188e+001,
	3.079730e+001,
	// albedo 1, turbidity 6
	1.151366e+000,
	2.333005e+000,
	-3.728627e+000,
	1.363374e+001,
	3.905894e+001,
	3.092599e+001,
	// albedo 1, turbidity 7
	1.101593e+000,
	2.299422e+000,
	-3.565787e+000,
	1.196745e+001,
	4.188472e+001,
	3.102755e+001,
	// albedo 1, turbidity 8
	1.038322e+000,
	2.083539e+000,
	-2.649585e+000,
	8.037389e+000,
	4.700869e+001,
	3.065948e+001,
	// albedo 1, turbidity 9
	9.596146e-001,
	1.671470e+000,
	-8.751538e-001,
	1.679772e+000,
	5.345784e+001,
	3.054520e+001,
	// albedo 1, turbidity 10
	8.640731e-001,
	9.858301e-001,
	1.854956e+000,
	-6.798097e+000,
	5.936468e+001,
	3.110255e+001,
};

double[][] datasetsXYZ;

double[][] datasetsXYZRad;

}


