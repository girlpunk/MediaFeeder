package xyz.foxocube.mediafeeder.androidtv.data.repositories

import xyz.foxocube.mediafeeder.androidtv.data.entities.VideoDetails

interface VideoRepository {
//    fun getFeaturedVideos(): Flow<MovieList>
//    fun getTrendingVideos(): Flow<MovieList>
//    fun getTop10Videos(): Flow<MovieList>
//    fun getNowPlayingVideos(): Flow<MovieList>
//    fun getVideoCategories(): Flow<MovieCategoryList>
//    suspend fun getVideoCategoryDetails(categoryId: String): MovieCategoryDetails
    suspend fun getVideoDetails(movieId: String): VideoDetails
//    suspend fun searchVideos(query: String): MovieList
//    fun getVideosWithLongThumbnail(): Flow<MovieList>
//    fun getVideos(): Flow<MovieList>
//    fun getPopularFilmsThisWeek(): Flow<MovieList>
//    fun getTVShows(): Flow<MovieList>
//    fun getBingeWatchDramas(): Flow<MovieList>
//    fun getFavouriteVideos(): Flow<MovieList>
}
