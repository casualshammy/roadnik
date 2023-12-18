//webpack.config.js
const path = require('path');

module.exports = {
  mode: "production", // development production https://webpack.js.org/configuration/mode/
  devtool: "inline-source-map",
  entry: {
    main: "./src/main.ts",
  },
  output: {
    path: path.resolve(__dirname, './'),
    filename: "dist/room/bundle.js" // <--- Will be compiled to this single file
  },
  resolve: {
    extensions: [".ts", ".tsx", ".js"],
  },
  module: {
    rules: [
      { 
        test: /\.tsx?$/,
        loader: "ts-loader"
      }
    ]
  },
  performance: {
    maxEntrypointSize: 3*1024*1024,
    maxAssetSize: 3*1024*1024
  },
};