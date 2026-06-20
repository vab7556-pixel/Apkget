package {{PACKAGE}};

import android.util.Log;
import java.util.HashMap;
import java.util.Map;

/**
 * أمثلة عملية لاستخدام Analytics Logger
 * 
 * هذا الملف يوضح كيفية استخدام نظام Analytics في تطبيقك
 */
public class AnalyticsUsageExample {
    
    private static final String TAG = "AnalyticsExample";
    private AnalyticsLogger analyticsLogger;
    
    public AnalyticsUsageExample(AnalyticsLogger analyticsLogger) {
        this.analyticsLogger = analyticsLogger;
    }
    
    /**
     * مثال 1: تسجيل تسجيل الدخول
     */
    public void trackUserLogin(String userId, String loginMethod) {
        Map<String, Object> data = new HashMap<>();
        data.put("user_id", userId);
        data.put("login_method", loginMethod);
        data.put("timestamp", System.currentTimeMillis());
        
        analyticsLogger.logEvent("user_login", data);
        Log.d(TAG, "User login tracked: " + userId);
    }
    
    /**
     * مثال 2: تسجيل تسجيل الخروج
     */
    public void trackUserLogout(String userId) {
        Map<String, Object> data = new HashMap<>();
        data.put("user_id", userId);
        data.put("logout_time", System.currentTimeMillis());
        
        analyticsLogger.logEvent("user_logout", data);
        Log.d(TAG, "User logout tracked: " + userId);
    }
    
    /**
     * مثال 3: تسجيل عرض الشاشة
     */
    public void trackScreenView(String screenName, long durationMs) {
        Map<String, Object> data = new HashMap<>();
        data.put("screen_name", screenName);
        data.put("duration_ms", durationMs);
        data.put("view_time", System.currentTimeMillis());
        
        analyticsLogger.logEvent("screen_viewed", data);
        Log.d(TAG, "Screen view tracked: " + screenName);
    }
    
    /**
     * مثال 4: تسجيل النقر على الزر
     */
    public void trackButtonClick(String buttonId, String buttonLabel) {
        Map<String, Object> data = new HashMap<>();
        data.put("button_id", buttonId);
        data.put("button_label", buttonLabel);
        data.put("click_time", System.currentTimeMillis());
        
        analyticsLogger.logEvent("button_clicked", data);
        Log.d(TAG, "Button click tracked: " + buttonId);
    }
    
    /**
     * مثال 5: تسجيل عملية شراء
     */
    public void trackPurchase(String productId, double amount, String currency) {
        Map<String, Object> data = new HashMap<>();
        data.put("product_id", productId);
        data.put("amount", amount);
        data.put("currency", currency);
        data.put("purchase_time", System.currentTimeMillis());
        
        analyticsLogger.logEvent("purchase_completed", data);
        Log.d(TAG, "Purchase tracked: " + productId + " - " + amount + " " + currency);
    }
    
    /**
     * مثال 6: تسجيل خطأ API
     */
    public void trackApiError(String endpoint, String errorMessage, Exception exception) {
        Map<String, Object> data = new HashMap<>();
        data.put("endpoint", endpoint);
        data.put("error_message", errorMessage);
        data.put("error_time", System.currentTimeMillis());
        
        analyticsLogger.logError("api_error", errorMessage, exception);
        Log.e(TAG, "API error tracked: " + endpoint);
    }
    
    /**
     * مثال 7: تسجيل خطأ التحقق من الصحة
     */
    public void trackValidationError(String fieldName, String errorMessage) {
        Map<String, Object> data = new HashMap<>();
        data.put("field_name", fieldName);
        data.put("error_message", errorMessage);
        
        analyticsLogger.logEvent("validation_error", data);
        Log.d(TAG, "Validation error tracked: " + fieldName);
    }
    
    /**
     * مثال 8: قياس أداء تحميل البيانات
     */
    public void trackDataLoadingPerformance(String dataType) {
        long startTime = System.currentTimeMillis();
        
        // محاكاة تحميل البيانات
        try {
            Thread.sleep(1000); // محاكاة عملية
        } catch (InterruptedException e) {
            e.printStackTrace();
        }
        
        long duration = System.currentTimeMillis() - startTime;
        analyticsLogger.logPerformance("data_loading_" + dataType, duration);
        Log.d(TAG, "Data loading performance tracked: " + duration + "ms");
    }
    
    /**
     * مثال 9: قياس أداء معالجة الصور
     */
    public void trackImageProcessingPerformance(String imageSize) {
        long startTime = System.currentTimeMillis();
        
        // محاكاة معالجة الصورة
        try {
            Thread.sleep(500); // محاكاة عملية
        } catch (InterruptedException e) {
            e.printStackTrace();
        }
        
        long duration = System.currentTimeMillis() - startTime;
        analyticsLogger.logPerformance("image_processing_" + imageSize, duration);
        Log.d(TAG, "Image processing performance tracked: " + duration + "ms");
    }
    
    /**
     * مثال 10: قياس أداء استعلام قاعدة البيانات
     */
    public void trackDatabaseQueryPerformance(String queryType) {
        long startTime = System.currentTimeMillis();
        
        // محاكاة استعلام قاعدة البيانات
        try {
            Thread.sleep(200); // محاكاة عملية
        } catch (InterruptedException e) {
            e.printStackTrace();
        }
        
        long duration = System.currentTimeMillis() - startTime;
        analyticsLogger.logPerformance("db_query_" + queryType, duration);
        Log.d(TAG, "Database query performance tracked: " + duration + "ms");
    }
    
    /**
     * مثال 11: تسجيل حدث مخصص
     */
    public void trackCustomEvent(String eventName, Map<String, Object> customData) {
        analyticsLogger.logEvent(eventName, customData);
        Log.d(TAG, "Custom event tracked: " + eventName);
    }
    
    /**
     * مثال 12: الحصول على إحصائيات الأحداث
     */
    public void printEventStatistics(String eventType) {
        org.json.JSONObject stats = analyticsLogger.getEventStats(eventType);
        Log.d(TAG, "Event statistics for " + eventType + ": " + stats.toString());
    }
    
    /**
     * مثال 13: الحصول على جميع الإحصائيات
     */
    public void printAllStatistics() {
        org.json.JSONObject allStats = analyticsLogger.getAllStats();
        Log.d(TAG, "All statistics: " + allStats.toString());
    }
    
    /**
     * مثال 14: الحصول على الأحداث حسب النوع
     */
    public void printEventsByType(String eventType) {
        org.json.JSONArray events = analyticsLogger.getEventsByType(eventType);
        Log.d(TAG, "Events of type " + eventType + ": " + events.toString());
    }
    
    /**
     * مثال 15: تنظيف البيانات القديمة
     */
    public void cleanupOldData() {
        // حذف الأحداث الأقدم من 30 يوم
        analyticsLogger.deleteOldEvents(30);
        Log.d(TAG, "Old events deleted");
    }
    
    /**
     * مثال 16: تصدير البيانات
     */
    public String exportAnalyticsData() {
        String exportedData = analyticsLogger.exportEventsAsJSON();
        Log.d(TAG, "Analytics data exported");
        return exportedData;
    }
    
    /**
     * مثال 17: مزامنة البيانات مع الخادم
     */
    public void syncAnalyticsToServer(String serverUrl) {
        analyticsLogger.syncToServer(serverUrl, new AnalyticsLogger.SyncCallback() {
            @Override
            public void onSyncSuccess(String message) {
                Log.d(TAG, "Sync success: " + message);
            }
            
            @Override
            public void onSyncError(String error) {
                Log.e(TAG, "Sync error: " + error);
            }
        });
    }
    
    /**
     * مثال 18: الحصول على معلومات التخزين
     */
    public void printStorageInfo() {
        int totalEvents = analyticsLogger.getTotalEventsCount();
        double sizeInMB = analyticsLogger.getLogsSizeInMB();
        
        Log.d(TAG, "Total events: " + totalEvents);
        Log.d(TAG, "Storage size: " + String.format("%.2f", sizeInMB) + " MB");
    }
    
    /**
     * مثال 19: تسجيل حدث معقد مع بيانات متعددة المستويات
     */
    public void trackComplexEvent() {
        Map<String, Object> userData = new HashMap<>();
        userData.put("user_id", "user_123");
        userData.put("user_name", "Ahmed");
        userData.put("user_email", "ahmed@example.com");
        
        Map<String, Object> deviceData = new HashMap<>();
        deviceData.put("device_model", android.os.Build.MODEL);
        deviceData.put("android_version", android.os.Build.VERSION.SDK_INT);
        deviceData.put("device_id", android.os.Build.ID);
        
        Map<String, Object> eventData = new HashMap<>();
        eventData.put("user", userData);
        eventData.put("device", deviceData);
        eventData.put("action", "complex_action");
        eventData.put("timestamp", System.currentTimeMillis());
        
        analyticsLogger.logEvent("complex_event", eventData);
        Log.d(TAG, "Complex event tracked");
    }
    
    /**
     * مثال 20: تسجيل سلسلة من الأحداث
     */
    public void trackUserJourney() {
        // الخطوة 1: فتح التطبيق
        trackScreenView("splash_screen", 2000);
        
        // الخطوة 2: تسجيل الدخول
        trackUserLogin("user_123", "email");
        
        // الخطوة 3: عرض الشاشة الرئيسية
        trackScreenView("home_screen", 5000);
        
        // الخطوة 4: النقر على زر
        trackButtonClick("explore_btn", "Explore");
        
        // الخطوة 5: عرض قائمة المنتجات
        trackScreenView("products_screen", 10000);
        
        // الخطوة 6: شراء منتج
        trackPurchase("product_123", 99.99, "USD");
        
        Log.d(TAG, "User journey tracked");
    }
}
