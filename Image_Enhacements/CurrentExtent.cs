using System;
using System.Linq;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using System.IO;
using System.Collections.Generic;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;


namespace Image_Enhacements
{
    internal class CurrentExtentStatistics : Button
    {

        private List<Double> CalculateBandStatistics(Raster raster, int bandIndex)
        {
            RasterCursor rasterCursor = raster.CreateCursor(500, 500);
            List<Double> blockValues = new List<Double>();
            do
            {
                PixelBlock currentPixelBlock = rasterCursor.Current;
                Array sourcePixeles = currentPixelBlock.GetPixelData(bandIndex, true);
                int pixelBlockHeight = currentPixelBlock.GetHeight();
                int pixelBlockWidth = currentPixelBlock.GetWidth();
                foreach (var valuePixel in sourcePixeles)
                    blockValues.Add(Convert.ToDouble(valuePixel));
            }
            while (rasterCursor.MoveNext());

            var typicalBlockValues = Statistical.ExcludeOuliers(blockValues, 0.0025);
            var statistics = Statistical.CalculateStatistics(typicalBlockValues);

            return statistics;
        }

        protected async override void OnClick()
        {

            // Map and scale validations
            var mapView = MapView.Active;
            if (mapView != null)
            {
                var scale = mapView.Camera.Scale;
                if (scale > 35000)
                {
                    MessageBox.Show(string.Format("Map extent is too big (Map Scale: {0}). Zoom in to scales under 35.000.", (int)scale));
                    return;
                }
            }
            else
            {
                return;
            }

            // Get selected layer
            var selectedLayer = mapView.GetSelectedLayers().OfType<BasicRasterLayer>().FirstOrDefault();
            if (selectedLayer != null)
            {
                // Clipping image 
                var progressDialog = new ProgressDialog("Getting statistics", "Cancel", 100, true);
                progressDialog.Show();
                var progressSource = new CancelableProgressorSource(progressDialog);

                var clippingResult = await QueuedTask.Run(() =>
                {
                    string tool_path = "management.clip";
                    string output = @"in_memory\raster";
                    var rectangle = string.Format("{0} {1} {2} {3}", mapView.Extent.XMin, mapView.Extent.YMin, mapView.Extent.XMax, mapView.Extent.YMax);

                    var args = Geoprocessing.MakeValueArray((RasterLayer)selectedLayer, "", output);
                    var envs = Geoprocessing.MakeEnvironmentArray(extent: rectangle);
                    var flags = GPExecuteToolFlags.None; // GPExecuteToolFlags.AddOutputsToMap;
                    var result = Geoprocessing.ExecuteToolAsync(tool_path, args, envs, progressSource.Progressor, flags);

                    return result;
                });
                progressDialog.Hide();

                // Getting statistics fro current extent
                await QueuedTask.Run(() =>
                {
                    var clippedRaster = clippingResult.Values[0];
                    string baseName = Path.GetFileName(clippedRaster);
                    string folderName = Path.GetDirectoryName(clippedRaster);
                    var geodatabasePath = new FileGeodatabaseConnectionPath(new Uri(folderName));
                    var geodatabase = new Geodatabase(geodatabasePath);
                    var rasterDataset = geodatabase.OpenDataset<RasterDataset>(baseName);
                    Raster raster = rasterDataset.CreateFullRaster();
                    CIMRasterRGBColorizer colorizer = (CIMRasterRGBColorizer)selectedLayer.GetColorizer();

                    // Computing statistics 
                    var redStatistics = CalculateBandStatistics(raster, colorizer.RedBandIndex);
                    var greenStatistics = CalculateBandStatistics(raster, colorizer.GreenBandIndex);
                    var blueStatistics = CalculateBandStatistics(raster, colorizer.BlueBandIndex);

                    if (colorizer != null)
                    {
                        colorizer.StretchStatsType = RasterStretchStatsType.GlobalStats;
                        colorizer.StretchType = RasterStretchType.Custom;

                        // Red Band statistics
                        colorizer.StretchStatsRed.mean = redStatistics[0];
                        colorizer.StretchStatsRed.min = redStatistics[1];
                        colorizer.StretchStatsRed.max = redStatistics[2];
                        colorizer.StretchStatsRed.stddev = redStatistics[3];

                        // Green Band statistics
                        colorizer.StretchStatsGreen.mean = greenStatistics[0];
                        colorizer.StretchStatsGreen.min = greenStatistics[1];
                        colorizer.StretchStatsGreen.max = greenStatistics[2];
                        colorizer.StretchStatsGreen.stddev = greenStatistics[3];

                        // Bue band statistics
                        colorizer.StretchStatsBlue.mean = blueStatistics[0];
                        colorizer.StretchStatsBlue.min = blueStatistics[1];
                        colorizer.StretchStatsBlue.max = blueStatistics[2];
                        colorizer.StretchStatsBlue.stddev = blueStatistics[3];

                        selectedLayer.SetColorizer(colorizer);
                    }
                });
            }
            else
            {
                MessageBox.Show("There is no Raster layer selected in TOC.");
                return;
            }
        }
    }
}