/// <binding Clean='clean' />
"use strict";

var gulp = require("gulp"),
    sass = require("gulp-sass"),
    rename = require("gulp-rename"),
    cssmin = require("gulp-cssnano"),
    prefix = require("gulp-autoprefixer"),
    sourcemaps = require("gulp-sourcemaps"),
    gulpWebpack = require("webpack-stream");

var sassOptions = {
    outputStyle: "expanded"
};

gulp.task("sass:mdb",
    function()
    {
        return gulp.src("./node_modules/mdbootstrap/scss/mdb.scss")
            .pipe(sourcemaps.init())
            .pipe(sass(sassOptions))
            .pipe(prefix())
            .pipe(sourcemaps.write())
            .pipe(gulp.dest("./wwwroot/styles/"))
            .pipe(cssmin())
            .pipe(rename({ suffix: ".min" }))
            .pipe(gulp.dest("./wwwroot/styles/"));
    });

gulp.task("react:dev",
    function()
    {
        var config = require('./webpack.config');
        config.mode = 'development';
        delete config.optimization;
        return gulp.src('./src/**/*.ts')
            .pipe(gulpWebpack(config, require('webpack')))
            .pipe(gulp.dest('./wwwroot/dist/'));
    });

gulp.task("react:prod",
    function()
    {
        var config = require('./webpack.config');
        config.mode = 'production';
        delete config.devtool;
        return gulp.src('./src/**/*.ts')
            .pipe(gulpWebpack(config, require('webpack')))
            .pipe(gulp.dest('./wwwroot/dist/'));
    });
