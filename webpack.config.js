const webpack = require('webpack');
const HtmlWebpackPlugin = require('html-webpack-plugin');

module.exports = {
  entry: './src/main.ts',
  module: {
    rules: [
      {
        test: /\.ts$/,
        use: ['ts-loader', 'angular2-template-loader'],
        exclude: /node_modules/
      },
      { //this rule will only be used for any vendors
        test: /\.css$/,
        loaders: ['style-loader', 'css-loader'],
        include: [/node_modules/]
      },
      {
        test: /\.css$/,
        loaders: ['to-string-loader', 'css-loader'],
        exclude: [/node_modules/] //add this line so we ignore css coming from node_modules
      },
      {
        test: /\.(html)$/,
        loader: 'raw-loader'
      }
    ]
  },
  resolve: {
    extensions: ['.ts', '.js', '.jsx', '.css']
  },
  plugins: [
    new HtmlWebpackPlugin({
      template: './src/index.html',
      filename: 'index.html',
      inject: 'body'
    }),
    new webpack.DefinePlugin({
      // global app config object
      config: JSON.stringify({
        apiUrl: 'http://localhost:50091'
      })
    })
  ],
  optimization: {
    splitChunks: {
      chunks: 'all',
    },
    runtimeChunk: true
  },
  devServer: {
    historyApiFallback: true
  }
};