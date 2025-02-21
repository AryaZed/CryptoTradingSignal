using CryptoTradingSignal.Model;
using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CryptoTradingSignal.ML
{
    public class MLModel
    {
        private readonly MLContext _mlContext;
        private ITransformer _model;
        private const string ModelPath = "crypto_trading_model.zip";

        public MLModel()
        {
            _mlContext = new MLContext(seed: 42); // Ensures reproducibility
            LoadModel();
        }

        /// <summary>
        /// Train and save the ML model using real historical crypto data.
        /// </summary>
        public void TrainModel(List<CryptoHistoricalData> trainingData)
        {
            var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

            // Feature Engineering
            var pipeline = _mlContext.Transforms.Conversion.MapValueToKey("Signal")
                .Append(_mlContext.Transforms.Concatenate("Features", new[]
                {
                    "Open", "High", "Low", "Close", "Volume", "SMA", "RSI", "MACD", "Volatility", "Momentum", "TrendStrength"
                }))
                .Append(_mlContext.Transforms.NormalizeMeanVariance("Features")) // Normalization
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("Signal")) // Classification
                .Append(_mlContext.MulticlassClassification.Trainers.LightGbm("Signal", "Features")) // Using LightGBM for better predictions
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            // Train Model
            _model = pipeline.Fit(dataView);

            Console.WriteLine("✅ Model Trained Successfully!");
            SaveModel();
        }

        /// <summary>
        /// Predicts whether to Buy, Hold, or Sell a cryptocurrency.
        /// </summary>
        public string Predict(float open, float high, float low, float close, float volume, float sma, float rsi, float macd, float volatility, float momentum, float trendStrength)
        {
            if (_model == null) return "Unknown";

            var predictionEngine = _mlContext.Model.CreatePredictionEngine<CryptoHistoricalData, CryptoPrediction>(_model);
            var prediction = predictionEngine.Predict(new CryptoHistoricalData
            {
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume,
                SMA = sma,
                RSI = rsi,
                MACD = macd,
                Volatility = volatility,
                Momentum = momentum,
                TrendStrength = trendStrength
            });

            return prediction.Signal;
        }

        /// <summary>
        /// Saves the trained ML model to a file.
        /// </summary>
        private void SaveModel()
        {
            _mlContext.Model.Save(_model, null, ModelPath);
            Console.WriteLine($"✅ Model saved to {ModelPath}");
        }

        /// <summary>
        /// Loads the ML model from a saved file.
        /// </summary>
        private void LoadModel()
        {
            if (File.Exists(ModelPath))
            {
                _model = _mlContext.Model.Load(ModelPath, out _);
                Console.WriteLine($"✅ Model loaded from {ModelPath}");
            }
            else
            {
                Console.WriteLine("⚠️ No saved model found. Please train the model first.");
            }
        }
    }

    public class CryptoPrediction
    {
        [ColumnName("PredictedLabel")]
        public string Signal { get; set; }
    }
}
