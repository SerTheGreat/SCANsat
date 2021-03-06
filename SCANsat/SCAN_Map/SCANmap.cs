#region license
/*
 * [Scientific Committee on Advanced Navigation]
 * 			S.C.A.N. Satellite
 * 
 * SCANmap - makes maps from data
 *
 * Copyright (c)2013 damny;
 * Copyright (c)2014 technogeeky <technogeeky@gmail.com>;
 * Copyright (c)2014 (Your Name Here) <your email here>; see LICENSE.txt for licensing details.
*/
#endregion

using System;
using System.Linq;
using System.IO;
using UnityEngine;
using SCANsat.SCAN_Platform.Palettes;
using SCANsat.SCAN_Platform.Logging;
using SCANsat.SCAN_Data;
using SCANsat.SCAN_UI.UI_Framework;
using palette = SCANsat.SCAN_UI.UI_Framework.SCANpalette;

namespace SCANsat.SCAN_Map
{
	public class SCANmap
	{
		internal SCANmap(CelestialBody Body, bool Cache, mapSource s)
		{
			body = Body;
			mSource = s;
			pqs = body.pqsController != null;
			biomeMap = body.BiomeMap != null;
			data = SCANUtil.getData(body);
			if (data == null)
			{
				data = new SCANdata(body);
				SCANcontroller.controller.addToBodyData(body, data);
			}
			cache = Cache;
		}

		internal SCANmap()
		{
		}

		#region Public Accessors

		public double MapScale
		{
			get { return mapscale; }
			internal set
			{
				mapscale = value;
				resourceMapScale = (mapwidth / resourceMapWidth) * mapscale;
			}
		}

		public double Lon_Offset
		{
			get { return lon_offset; }
		}

		public double Lat_Offset
		{
			get { return lat_offset; }
		}

		public double CenteredLong
		{
			get { return centeredLong; }
		}

		public double CenteredLat
		{
			get { return centeredLat; }
		}

		public int MapWidth
		{
			get { return mapwidth; }
		}

		public int MapHeight
		{
			get { return mapheight; }
		}

		public mapType MType
		{
			get { return mType; }
		}

		public mapSource MSource
		{
			get { return mSource; }
		}

		public Texture2D Map
		{
			get { return map; }
			internal set { map = value; }
		}

		public CelestialBody Body
		{
			get { return body; }
		}

		public SCANresourceGlobal Resource
		{
			get { return resource; }
			internal set { resource = value; }
		}

		public bool ResourceActive
		{
			get { return resourceActive; }
		}

		public SCANmapLegend MapLegend
		{
			get { return mapLegend; }
			internal set { mapLegend = value; }
		}

		public MapProjection Projection
		{
			get { return projection; }
		}

		internal float[,] Big_HeightMap
		{
			get { return big_heightmap; }
		}

		public bool UseCustomRange
		{
			get { return useCustomRange; }
		}

		public float CustomMin
		{
			get { return customMin; }
		}

		public float CustomMax
		{
			get { return customMax; }
		}

		public float CustomRange
		{
			get { return customRange; }
		}

		#endregion

		#region Big Map methods and fields

		/* MAP: Big Map height map caching */
		private float[,] big_heightmap;
		private bool cache;
		private double centeredLong, centeredLat;

		private void terrainHeightToArray(double lon, double lat, int ilon, int ilat)
		{
			float alt = 0f;
			alt = (float)SCANUtil.getElevation(body, lon, lat);
			if (alt == 0f)
				alt = -0.001f;
			big_heightmap[ilon, ilat] = alt;
		}

		/* MAP: Projection methods for converting planet coordinates to the rectangular texture */
		private MapProjection projection = MapProjection.Rectangular;

		internal void setProjection(MapProjection p)
		{
			if (projection == p)
				return;
			projection = p;
		}

		internal double projectLongitude(double lon, double lat)
		{
			lon = (lon + 3600 + 180) % 360 - 180;
			lat = (lat + 1800 + 90) % 180 - 90;
			switch (projection)
			{
				case MapProjection.KavrayskiyVII:
					lon = Mathf.Deg2Rad * lon;
					lat = Mathf.Deg2Rad * lat;
					lon = (3.0f * lon / 2.0f / Math.PI) * Math.Sqrt(Math.PI * Math.PI / 3.0f - lat * lat);
					return Mathf.Rad2Deg * lon;
				case MapProjection.Polar:
					lon = Mathf.Deg2Rad * lon;
					lat = Mathf.Deg2Rad * lat;
					if (lat < 0)
					{
						lon = 1.3 * Math.Cos(lat) * Math.Sin(lon) - Math.PI / 2;
					}
					else
					{
						lon = 1.3 * Math.Cos(lat) * Math.Sin(lon) + Math.PI / 2;
					}
					return Mathf.Rad2Deg * lon;
				default:
					return lon;
			}
		}

		internal double projectLatitude(double lon, double lat)
		{
			lon = (lon + 3600 + 180) % 360 - 180;
			lat = (lat + 1800 + 90) % 180 - 90;
			switch (projection)
			{
				case MapProjection.Polar:
					lon = Mathf.Deg2Rad * lon;
					lat = Mathf.Deg2Rad * lat;
					if (lat < 0)
					{
						lat = 1.3 * Math.Cos(lat) * Math.Cos(lon);
					}
					else
					{
						lat = -1.3 * Math.Cos(lat) * Math.Cos(lon);
					}
					return Mathf.Rad2Deg * lat;
				default:
					return lat;
			}
		}

		internal double unprojectLongitude(double lon, double lat)
		{
			if (lat > 90)
			{
				lat = 180 - lat;
				lon += 180;
			}
			else if (lat < -90)
			{
				lat = -180 - lat;
				lon += 180;
			}
			lon = (lon + 3600 + 180) % 360 - 180;
			lat = (lat + 1800 + 90) % 180 - 90;
			switch (projection)
			{
				case MapProjection.KavrayskiyVII:
					lon = Mathf.Deg2Rad * lon;
					lat = Mathf.Deg2Rad * lat;
					lon = lon / Math.Sqrt(Mathf.PI * Math.PI / 3.0f - lat * lat) * 2.0f * Math.PI / 3.0f;
					return Mathf.Rad2Deg * lon;
				case MapProjection.Polar:
					lon = Mathf.Deg2Rad * lon;
					lat = Mathf.Deg2Rad * lat;
					double lat0 = Math.PI / 2;
					if (lon < 0)
					{
						lon += Math.PI / 2;
						lat0 = -Math.PI / 2;
					}
					else
					{
						lon -= Math.PI / 2;
					}
					lon /= 1.3;
					lat /= 1.3;
					double p = Math.Sqrt(lon * lon + lat * lat);
					double c = Math.Asin(p);
					lon = Math.Atan2((lon * Math.Sin(c)), (p * Math.Cos(lat0) * Math.Cos(c) - lat * Math.Sin(lat0) * Math.Sin(c)));
					lon = (Mathf.Rad2Deg * lon + 180) % 360 - 180;
					if (lon <= -180)
						lon = -180;
					return lon;
				default:
					return lon;
			}
		}

		internal double unprojectLatitude(double lon, double lat)
		{
			if (lat > 90)
			{
				lat = 180 - lat;
				lon += 180;
			}
			else if (lat < -90)
			{
				lat = -180 - lat;
				lon += 180;
			}
			lon = (lon + 3600 + 180) % 360 - 180;
			lat = (lat + 1800 + 90) % 180 - 90;
			switch (projection)
			{
				case MapProjection.Polar:
					lon = Mathf.Deg2Rad * lon;
					lat = Mathf.Deg2Rad * lat;
					double lat0 = Math.PI / 2;
					if (lon < 0)
					{
						lon += Math.PI / 2;
						lat0 = -Math.PI / 2;
					}
					else
					{
						lon -= Math.PI / 2;
					}
					lon /= 1.3;
					lat /= 1.3;
					double p = Math.Sqrt(lon * lon + lat * lat);
					double c = Math.Asin(p);
					lat = Math.Asin(Math.Cos(c) * Math.Sin(lat0) + (lat * Math.Sin(c) * Math.Cos(lat0)) / (p));
					return Mathf.Rad2Deg * lat;
				default:
					return lat;
			}
		}

		/* MAP: scaling, centering (setting origin), translating, etc */
		private double mapscale, lon_offset, lat_offset;
		private int mapwidth, mapheight;
		private Color32[] pix;
		private bool resourceActive;
		private float[,] resourceCache;
		private int resourceInterpolation = 4;
		private int resourceMapWidth = 4;
		private int resourceMapHeight = 2;
		private double resourceMapScale = 1;
		private bool randomEdges = true;
		private double[] biomeIndex;
		private Color32[] stockBiomeColor;
		private int startLine;
		private int stopLine;

		internal void setSize(int w, int h, int interpolation = 2, int start = 0, int stop = 0)
		{
			if (w == 0)
				w = 360 * (Screen.width / 360);
			if (w > 360 * 4)
				w = 360 * 4;
			mapwidth = w;
			pix = new Color32[mapwidth];
			biomeIndex = new double[mapwidth];
			stockBiomeColor = new Color32[mapwidth];
			mapscale = mapwidth / 360f;
			if (h <= 0)
				h = (int)(180 * mapscale);
			mapheight = h;
			startLine = start;
			stopLine = stop == 0 ? mapheight - 1 : stop;
			resourceMapWidth = mapwidth;
			resourceMapHeight = mapheight;
			resourceCache = new float[resourceMapWidth, resourceMapHeight];
			resourceInterpolation = interpolation;
			resourceMapScale = resourceMapWidth / 360;
			randomEdges = false;
			if (map != null)
			{
				if (mapwidth != map.width || mapheight != map.height)
					map = null;
			}
		}

		internal void setWidth(int w)
		{
			if (w == 0)
			{
				w = 360 * (int)(Screen.width / 360);
				if (w > 360 * 4)
					w = 360 * 4;
			}
			if (w < 360)
				w = 360;
			if (mapwidth == w)
				return;
			mapwidth = w;
			pix = new Color32[w];
			biomeIndex = new double[w];
			stockBiomeColor = new Color32[w];
			resourceMapHeight = SCANcontroller.controller.overlayMapHeight;
			resourceMapWidth = resourceMapHeight * 2;
			resourceInterpolation = SCANcontroller.controller.overlayInterpolation;
			resourceMapScale = resourceMapWidth / 360f;
			resourceCache = new float[resourceMapWidth, resourceMapHeight];
			randomEdges = true;
			mapscale = mapwidth / 360f;
			mapheight = (int)(w / 2);
			startLine = 0;
			stopLine = mapheight - 1;
			/* big map caching */
			big_heightmap = new float[mapwidth, mapheight];
			map = null;
			resetMap(resourceActive);
		}

		internal void centerAround(double lon, double lat)
		{
			if (projection == MapProjection.Polar)
			{
				double lo = projectLongitude(lon, lat);
				double la = projectLatitude(lon, lat);
				lon_offset = 180 + lo - (mapwidth / mapscale) / 2;
				lat_offset = 90 + la - (mapheight / mapscale) / 2;
			}
			else
			{
				lon_offset = 180 + lon - (mapwidth / mapscale) / 2;
				lat_offset = 90 + lat - (mapheight / mapscale) / 2;
			}
			centeredLong = lon;
			centeredLat = lat;
		}

		internal double scaleLatitude(double lat)
		{
			lat -= lat_offset;
			lat *= 180f / (mapheight / mapscale);
			return lat;
		}

		internal double scaleLongitude(double lon)
		{
			if (lon_offset < 0 && Math.Abs(lon_offset) < lon)
				lon -= 360;
			else if (lon_offset > 0 && Math.Abs(lon_offset) > lon)
				lon += 360;
			lon -= lon_offset;
			lon *= 360f / (mapwidth / mapscale);
			return lon;
		}

		private double unScaleLatitude(double lat)
		{
			lat -= lat_offset;
			lat += 90;
			lat *= mapscale;
			return lat;
		}

		private double unScaleLatitude(double lat, double scale)
		{
			lat -= lat_offset;
			lat += 90;
			lat *= scale;
			return lat;
		}

		private double unScaleLongitude(double lon)
		{
			lon -= lon_offset;
			lon += 180;
			lon *= mapscale;
			return lon;
		}

		private double unScaleLongitude(double lon, double scale)
		{
			lon -= lon_offset;
			lon = SCANUtil.fixLonShift(lon);
			lon += 180;
			lon *= scale;
			return lon;
		}

		private double fixUnscale(double value, int size)
		{
			if (value < 0)
				value = 0;
			else if (value >= (size - 0.5f))
				value = size - 1;
			return value;
		}

		/* MAP: internal state */
		private mapType mType;
		private mapSource mSource;
		private Texture2D map; // refs above: 214,215,216,232, below, and JSISCANsatRPM.
		private CelestialBody body = null; // all refs are below
		private SCANresourceGlobal resource;
		private SCANdata data;
		private SCANmapLegend mapLegend;
		private int mapstep; // all refs are below
		private double[] mapline; // all refs are below
		private bool pqs;
		private bool biomeMap;
		private float customMin;
		private float customMax;
		private float customRange;
		private bool useCustomRange;

		/* MAP: nearly trivial functions */
		public void setBody(CelestialBody b)
		{
			SCANcontroller.controller.unloadPQS(body, mSource);
			body = b;
			SCANcontroller.controller.loadPQS(body, mSource);
			pqs = body.pqsController != null;
			biomeMap = body.BiomeMap != null;
			data = SCANUtil.getData(body);

			/* clear cache in place if necessary */
			if (cache)
			{
				for (int x = 0; x < mapwidth; x++)
				{
					for (int y = 0; y < mapwidth / 2; y++)
						big_heightmap[x, y] = 0f;
				}
			}

			if (SCANconfigLoader.GlobalResource)
			{
				resourceActive = SCANcontroller.controller.map_ResourceOverlay;
				resource = SCANcontroller.getResourceNode(SCANcontroller.controller.resourceSelection);
				if (resource == null)
					resource = SCANcontroller.GetFirstResource;
				resource.CurrentBodyConfig(body.name);
			}
		}		

		public void setCustomRange(float min, float max)
		{
			useCustomRange = true;
			customMin = min;
			customMax = max;
			customRange = max - min;
		}

		internal bool isMapComplete()
		{
			if (map == null)
				return false;
			return mapstep >= map.height;
		}

		public void resetMap(bool resourceOn, bool setRes = true)
		{
			mapstep = -2;
			resourceActive = resourceOn;
			if (SCANconfigLoader.GlobalResource && setRes)
			{ //Make sure that a resource is initialized if necessary
				if (resource == null && body != null)
				{
					resource = SCANcontroller.getResourceNode(SCANcontroller.controller.resourceSelection);
					if (resource == null)
						resource = SCANcontroller.GetFirstResource;
					resource.CurrentBodyConfig(body.name);
				}
				resetResourceMap();
			}
		}

		public void resetMap(mapType mode, bool Cache, bool resourceOn, bool setRes = true)
		{
			mType = mode;
			cache = Cache;
			resetMap(resourceOn, setRes);
		}

		public void resetResourceMap()
		{
			if (mSource != mapSource.ZoomMap)
			{
				if (SCANcontroller.controller.overlayMapHeight != resourceMapHeight)
				{
					resourceMapHeight = SCANcontroller.controller.overlayMapHeight;
					resourceMapWidth = resourceMapHeight * 2;
					resourceMapScale = resourceMapWidth / 360f;
					resourceCache = new float[resourceMapWidth, resourceMapHeight];
				}

				if (SCANcontroller.controller.overlayInterpolation != resourceInterpolation)
					resourceInterpolation = SCANcontroller.controller.overlayInterpolation;
			}

			for (int i = 0; i < resourceMapWidth; i++ )
			{
				for (int j = 0; j < resourceMapHeight; j++)
				{
					resourceCache[i, j] = 0;
				}
			}
		}

		/* MAP: export: PNG file */
		private SCANmapExporter exporter;

		internal void exportPNG()
		{
			if (exporter == null)
			{
				UnityEngine.GameObject obj = new GameObject();

				exporter = obj.gameObject.AddComponent<SCANmapExporter>();
			}

			if (exporter.Exporting)
				return;

			exporter.exportPNG(this, data);
		}

		#endregion

		#region Big Map Texture Generator

		/* MAP: build: map to Texture2D */
		internal Texture2D getPartialMap()
		{
			if (data == null)
				return new Texture2D(1, 1);

			System.Random r = new System.Random(ResourceScenario.Instance.gameSettings.Seed);

			bool resourceOn = false;
			bool mapHidden = mapstep < startLine || mapstep > stopLine;

			if (map == null)
			{
				map = new Texture2D(mapwidth, mapheight, TextureFormat.ARGB32, false);
				pix = map.GetPixels32();
				for (int i = 0; i < pix.Length; ++i)
					pix[i] = palette.Clear;
				map.SetPixels32(pix);
				mapline = new double[map.width];
				pix = new Color32[mapwidth];
			}
			else if (mapstep >= map.height)
			{
				return map;
			}

			if (palette.redline == null || palette.redline.Length != map.width)
			{
				palette.redline = new Color32[map.width];
				for (int i = 0; i < palette.redline.Length; ++i)
					palette.redline[i] = palette.Red;
			}

			resourceOn = resourceActive && SCANconfigLoader.GlobalResource && resource != null;

			if (mapstep <= -2)
			{
				if (resourceOn)
					SCANuiUtil.generateResourceCache(ref resourceCache, resourceMapHeight, resourceMapWidth, resourceInterpolation, resourceMapScale, this);

				mapstep++;
				return map;
			}

			if (mapstep <= -1)
			{
				if (resourceOn)
				{
					for (int i = resourceInterpolation / 2; i >= 1; i /= 2)
					{
						SCANuiUtil.interpolate(resourceCache, resourceMapHeight, resourceMapWidth, i, i, i, r, randomEdges, mSource == mapSource.ZoomMap);
						SCANuiUtil.interpolate(resourceCache, resourceMapHeight, resourceMapWidth, 0, i, i, r, randomEdges, mSource == mapSource.ZoomMap);
						SCANuiUtil.interpolate(resourceCache, resourceMapHeight, resourceMapWidth, i, 0, i, r, randomEdges, mSource == mapSource.ZoomMap);
					}
				}
			}

			for (int i = 0; i < map.width; i++)
			{
				/* Introduce altimetry check here; Use unprojected lat/long coordinates
				 * All cached altimetry data stored in a single 2D array in rectangular format
				 * Pull altimetry data from cache after unprojection
				 */

				double cacheLat = ((mapstep + 1) * 1.0f / mapscale) - 90f + lat_offset;
				double lon = (i * 1.0f / mapscale) - 180f + lon_offset;

				if (body.pqsController != null && cache && mapstep + 1 < map.height)
				{
					if (big_heightmap[i, mapstep + 1] == 0f)
					{
						if (SCANUtil.isCovered(lon, cacheLat, data, SCANtype.Altimetry))
							terrainHeightToArray(lon, cacheLat, i, mapstep + 1);
					}
				}

				if (mapstep < 0)
					continue;

				if (mapHidden)
					continue;

				if (mType != mapType.Biome || !biomeMap)
					continue;

				double lat = (mapstep * 1.0f / mapscale) - 90f + lat_offset;
				double la = lat, lo = lon;
				lat = unprojectLatitude(lo, la);
				lon = unprojectLongitude(lo, la);

				if (double.IsNaN(lat) || double.IsNaN(lon) || lat < -90 || lat > 90 || lon < -180 || lon > 180)
				{
					stockBiomeColor[i] = palette.clear;
					biomeIndex[i] = 0;
					continue;
				}

				if (SCANcontroller.controller.useStockBiomes && SCANcontroller.controller.colours == 0)
				{
					stockBiomeColor[i] = SCANUtil.getBiome(body, lon, lat).mapColor;
					if (SCANcontroller.controller.biomeBorder)
						biomeIndex[i] = SCANUtil.getBiomeIndexFraction(body, lon, lat);
				}
				else
					biomeIndex[i] = SCANUtil.getBiomeIndexFraction(body, lon, lat);
			}

			if (mapstep <= -1)
			{
				mapstep++;
				return map;
			}

			for (int i = 0; i < map.width; i++)
			{
				if (mapHidden)
				{
					pix[i] = palette.Clear;
					continue;
				}

				Color32 baseColor = palette.Grey;
				pix[i] = baseColor;
				int scheme = SCANcontroller.controller.colours;
				float projVal = 0f;
				double lat = (mapstep * 1.0f / mapscale) - 90f + lat_offset;
				double lon = (i * 1.0f / mapscale) - 180f + lon_offset;
				double la = lat, lo = lon;
				lat = unprojectLatitude(lo, la);
				lon = unprojectLongitude(lo, la);

				if (double.IsNaN(lat) || double.IsNaN(lon) || lat < -90 || lat > 90 || lon < -180 || lon > 180)
				{
					pix[i] = palette.Clear;
					continue;
				}

				switch (mType)
				{
					case mapType.Altimetry:
						{
							if (!pqs)
							{
								baseColor = palette.lerp(palette.Black, palette.White, UnityEngine.Random.value);
							}
							else if (SCANUtil.isCovered(lon, lat, data, SCANtype.Altimetry))
							{
								projVal = terrainElevation(lon, lat, mapwidth, mapheight, big_heightmap, cache, data, out scheme);
								if (useCustomRange)
									baseColor = palette.heightToColor(projVal, scheme, data.TerrainConfig, customMin, customMax, customRange, useCustomRange);
								else
									baseColor = palette.heightToColor(projVal, scheme, data.TerrainConfig);
							}
							break;
						}
					case mapType.Slope:
						{
							if (!pqs)
							{
								baseColor = palette.lerp(palette.Black, palette.White, UnityEngine.Random.value);
							}
							else if (SCANUtil.isCovered(lon, lat, data, SCANtype.Altimetry))
							{
								projVal = terrainElevation(lon, lat, mapwidth, mapheight, big_heightmap, cache, data, out scheme);
								if (mapstep >= 0)
								{
									// This doesn't actually calculate the slope per se, but it's faster
									// than asking for yet more elevation data. Please don't use this
									// code to operate nuclear power plants or rockets.
									double v1 = mapline[i];
									if (i > 0)
										v1 = Math.Max(v1, mapline[i - 1]);
									if (i < mapline.Length - 1)
										v1 = Math.Max(v1, mapline[i + 1]);
									float v = Mathf.Clamp((float)Math.Abs(projVal - v1) / 1000f, 0, 2f);
									if (SCANcontroller.controller.colours == 1)
									{
										baseColor = palette.lerp(palette.Black, palette.White, v / 2f);
									}
									else
									{
										if (v < SCANcontroller.controller.slopeCutoff)
										{
											baseColor = palette.lerp(SCANcontroller.controller.lowSlopeColorOne, SCANcontroller.controller.highSlopeColorOne, v / SCANcontroller.controller.slopeCutoff);
										}
										else
										{
											baseColor = palette.lerp(SCANcontroller.controller.lowSlopeColorTwo, SCANcontroller.controller.highSlopeColorTwo, (v - SCANcontroller.controller.slopeCutoff) / (2 -SCANcontroller.controller.slopeCutoff));
										}
									}
								}
								mapline[i] = projVal;
							}
							break;
						}
					case mapType.Biome:
						{
							if (!biomeMap)
							{
								baseColor = palette.lerp(palette.Black, palette.White, UnityEngine.Random.value);
							}
							else if (SCANUtil.isCovered(lon, lat, data, SCANtype.Biome))
							{
								Color32 biome = palette.Grey;
								if (SCANcontroller.controller.colours == 1)
								{
									if ((i > 0 && mapline[i - 1] != biomeIndex[i]) || (mapstep > 0 && mapline[i] != biomeIndex[i]))
									{
										biome = palette.White;
									}
									else
									{
										biome = palette.lerp(palette.Black, palette.White, (float)biomeIndex[i]);
									}
								}
								else
								{
									Color32 elevation = palette.Grey;
									if (SCANcontroller.controller.biomeTransparency > 0)
									{
										if (!pqs)
										{
											elevation = palette.Grey;
										}
										else if (SCANUtil.isCovered(lon, lat, data, SCANtype.Altimetry))
										{
											projVal = terrainElevation(lon, lat, mapwidth, mapheight, big_heightmap, cache, data, out scheme);
											if (useCustomRange)
												elevation = palette.lerp(palette.Black, palette.White, Mathf.Clamp(projVal + (-1f * customMin), 0, customRange) / customRange);
											else
												elevation = palette.lerp(palette.Black, palette.White, Mathf.Clamp(projVal + (-1f * data.TerrainConfig.MinTerrain), 0, data.TerrainConfig.TerrainRange) / data.TerrainConfig.TerrainRange);
										}
									}

									if (SCANcontroller.controller.biomeBorder && ((i > 0 && mapline[i - 1] != biomeIndex[i]) || (mapstep > 0 && mapline[i] != biomeIndex[i])))
									{
										biome = palette.White;
									}
									else if (SCANcontroller.controller.useStockBiomes)
									{
										biome = palette.lerp(stockBiomeColor[i], elevation, SCANcontroller.controller.biomeTransparency / 100f);
									}
									else
									{
										biome = palette.lerp(palette.lerp(SCANcontroller.controller.lowBiomeColor32, SCANcontroller.controller.highBiomeColor32, (float)biomeIndex[i]), elevation, SCANcontroller.controller.biomeTransparency / 100f);
									}
								}

								baseColor = biome;
								mapline[i] = biomeIndex[i];
							}
							break;
						}
				}

				if (resourceOn)
				{
					float abundance = 0;
					switch (projection)
					{
						case MapProjection.Rectangular:
							{
								abundance = getResoureCache(lo, la);
								break;
							}
						case MapProjection.KavrayskiyVII:
							{
								abundance = getResoureCache(lon, lat);
								break;
							}
						case MapProjection.Polar:
							{
								if (mSource == mapSource.ZoomMap)
									abundance = resourceCache[Mathf.RoundToInt(i * (resourceMapWidth / mapwidth)), Mathf.RoundToInt(mapstep * (resourceMapWidth / mapwidth))];
								else
									abundance = getResoureCache(lon, lat);
								break;
							}
					}
					pix[i] = SCANuiUtil.resourceToColor(baseColor, resource, abundance, data, lon, lat);
				}
				else
					pix[i] = baseColor;
			}

			if (mapstep >= 0)
				map.SetPixels32(0, mapstep, map.width, 1, pix);

			mapstep++;

			if (mapstep % 10 == 0 || mapstep >= map.height)
			{
				if (mapstep < map.height - 1)
					map.SetPixels32(0, mapstep, map.width, 1, palette.redline);

				map.Apply();
			}

			return map;
		}

		/* Calculates the terrain elevation based on scanning coverage; fetches data from elevation cache if possible */
		private float terrainElevation(double Lon, double Lat, int w, int h, float[,] heightMap, bool c, SCANdata Data, out int Scheme, bool exporting = false)
		{
			float elevation = 0f;
			Scheme = SCANcontroller.controller.colours;
			if (SCANUtil.isCovered(Lon, Lat, Data, SCANtype.AltimetryHiRes))
			{
				if (c)
				{
					double lon = fixUnscale(unScaleLongitude(Lon), w);
					double lat = fixUnscale(unScaleLatitude(Lat), h);
					elevation = heightMap[Mathf.RoundToInt((float)lon), Mathf.RoundToInt((float)lat)];
					if (elevation == 0f && !exporting)
						elevation = (float)SCANUtil.getElevation(body, Lon, Lat);
				}
				else
					elevation = (float)SCANUtil.getElevation(body, Lon, Lat);
			}
			else
			{
				if (c)
				{
					double lon = fixUnscale(unScaleLongitude(Lon), w);
					double lat = fixUnscale(unScaleLatitude(Lat), h);
					elevation = heightMap[((int)(lon * 5)) / 5, ((int)(lat * 5)) / 5];
					if (elevation == 0f && !exporting)
						elevation = (float)SCANUtil.getElevation(body, ((int)(Lon * 5)) / 5, ((int)(Lat * 5)) / 5);
				}
				else
					elevation = (float)SCANUtil.getElevation(body, ((int)(Lon * 5)) / 5, ((int)(Lat * 5)) / 5);
				Scheme = 1;
			}

			return elevation;
		}

		public float terrainElevation(double Lon, double Lat, int W, int H, float[,] heightMap, SCANdata Data, bool export = false)
		{
			int i = 0;

			return terrainElevation(Lon, Lat, W, H, heightMap, true, Data, out i, export);
		}

		private float getResoureCache(double Lon, double Lat)
		{
			double resourceLat = fixUnscale(unScaleLatitude(Lat, resourceMapScale), resourceMapHeight);
			double resourceLon = fixUnscale(unScaleLongitude(Lon, resourceMapScale), resourceMapWidth);
			return resourceCache[Mathf.RoundToInt((float)resourceLon), Mathf.RoundToInt((float)resourceLat)];
		}

		#endregion

	}
}
