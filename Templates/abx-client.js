/**
 * ABX Client Library v1.0
 * مكتبة JavaScript للتفاعل مع Advanced Background Extensions
 * 
 * الاستخدام:
 * <script src="abx-client.js"></script>
 * <script>
 *   ABXClient.init();
 *   ABXClient.startMonitoring();
 * </script>
 */

class ABXClient {
    constructor() {
        this.isInitialized = false;
        this.eventListeners = new Map();
        this.monitoringActive = false;
        this.lastSystemReport = null;
        
        // ربط الأحداث
        this.setupEventHandlers();
    }
    
    /**
     * تهيئة ABX Client
     */
    static init() {
        if (!window.ABXClientInstance) {
            window.ABXClientInstance = new ABXClient();
        }
        return window.ABXClientInstance;
    }
    
    /**
     * إعداد معالجات الأحداث
     */
    setupEventHandlers() {
        // إنشاء كائن ABX العام للتفاعل مع Android
        window.ABX = {
            onEvent: (event, data) => {
                this.handleABXEvent(event, data);
            }
        };
        
        // إنشاء كائن ABXManager للتفاعل مع Android
        window.ABXManager = window.ABXManager || {};
    }
    
    /**
     * معالجة أحداث ABX من Android
     */
    handleABXEvent(event, data) {
        console.log(`[ABX] Event: ${event}`, data);
        
        try {
            let parsedData = data;
            if (typeof data === 'string') {
                try {
                    parsedData = JSON.parse(data);
                } catch (e) {
                    // البيانات نصية
                }
            }
            
            // تشغيل المستمعين المسجلين
            if (this.eventListeners.has(event)) {
                this.eventListeners.get(event).forEach(callback => {
                    try {
                        callback(parsedData);
                    } catch (error) {
                        console.error(`[ABX] Error in event listener for ${event}:`, error);
                    }
                });
            }
            
            // معالجة الأحداث الخاصة
            this.handleSpecialEvents(event, parsedData);
            
        } catch (error) {
            console.error(`[ABX] Error handling event ${event}:`, error);
        }
    }
    
    /**
     * معالجة الأحداث الخاصة
     */
    handleSpecialEvents(event, data) {
        switch (event) {
            case 'abx_monitoring_started':
                this.monitoringActive = true;
                this.showNotification('ABX Monitoring Started', 'System monitoring is now active', 'success');
                break;
                
            case 'abx_monitoring_stopped':
                this.monitoringActive = false;
                this.showNotification('ABX Monitoring Stopped', 'System monitoring has been stopped', 'info');
                break;
                
            case 'abx_system_report':
                this.lastSystemReport = data;
                this.updateSystemDashboard(data);
                break;
                
            case 'abx_performance_warning':
                this.showNotification('Performance Warning', data, 'warning');
                break;
                
            case 'abx_battery_warning':
                this.showNotification('Battery Warning', data, 'warning');
                break;
                
            case 'abx_error':
                this.showNotification('ABX Error', data, 'error');
                break;
        }
    }
    
    /**
     * تسجيل مستمع للأحداث
     */
    addEventListener(event, callback) {
        if (!this.eventListeners.has(event)) {
            this.eventListeners.set(event, []);
        }
        this.eventListeners.get(event).push(callback);
    }
    
    /**
     * إزالة مستمع الأحداث
     */
    removeEventListener(event, callback) {
        if (this.eventListeners.has(event)) {
            const listeners = this.eventListeners.get(event);
            const index = listeners.indexOf(callback);
            if (index > -1) {
                listeners.splice(index, 1);
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════════════
    //                              ABX Control Methods
    // ═══════════════════════════════════════════════════════════════════════════════════════
    
    /**
     * بدء المراقبة المتقدمة
     */
    startAdvancedMonitoring(config = {}) {
        const defaultConfig = {
            interval: 30,
            notifications: true,
            detailed_logging: false,
            performance_tracking: true
        };
        
        const finalConfig = { ...defaultConfig, ...config };
        
        if (window.ABXManager && window.ABXManager.startAdvancedMonitoring) {
            window.ABXManager.startAdvancedMonitoring(JSON.stringify(finalConfig));
        } else {
            console.warn('[ABX] ABXManager not available');
        }
    }
    
    /**
     * إيقاف المراقبة المتقدمة
     */
    stopAdvancedMonitoring() {
        if (window.ABXManager && window.ABXManager.stopAdvancedMonitoring) {
            window.ABXManager.stopAdvancedMonitoring();
        }
    }
    
    /**
     * الحصول على تقرير النظام
     */
    getSystemReport() {
        if (window.ABXManager && window.ABXManager.getSystemReport) {
            window.ABXManager.getSystemReport();
        }
    }
    
    /**
     * جدولة مهمة متقدمة
     */
    scheduleAdvancedTask(taskId, config) {
        if (window.ABXManager && window.ABXManager.scheduleAdvancedTask) {
            window.ABXManager.scheduleAdvancedTask(taskId, JSON.stringify(config));
        }
    }
    
    /**
     * إلغاء مهمة مجدولة
     */
    cancelScheduledTask(taskId) {
        if (window.ABXManager && window.ABXManager.cancelScheduledTask) {
            window.ABXManager.cancelScheduledTask(taskId);
        }
    }
    
    /**
     * تنفيذ مهمة فورية
     */
    executeImmediateTask(taskType, parameters = {}) {
        if (window.ABXManager && window.ABXManager.executeImmediateTask) {
            window.ABXManager.executeImmediateTask(taskType, JSON.stringify(parameters));
        }
    }
    
    /**
     * تحديث إعدادات ABX
     */
    updateSettings(settings) {
        if (window.ABXManager && window.ABXManager.updateSettings) {
            window.ABXManager.updateSettings(JSON.stringify(settings));
        }
    }
    
    /**
     * الحصول على المهام المجدولة
     */
    getScheduledTasks() {
        if (window.ABXManager && window.ABXManager.getScheduledTasks) {
            window.ABXManager.getScheduledTasks();
        }
    }
    
    /**
     * بدء مراقبة الأداء
     */
    startPerformanceMonitoring(config = {}) {
        if (window.ABXManager && window.ABXManager.startPerformanceMonitoring) {
            window.ABXManager.startPerformanceMonitoring(JSON.stringify(config));
        }
    }
    
    /**
     * بدء مراقبة البطارية
     */
    startBatteryMonitoring() {
        if (window.ABXManager && window.ABXManager.startBatteryMonitoring) {
            window.ABXManager.startBatteryMonitoring();
        }
    }
    
    /**
     * بدء مراقبة الشبكة
     */
    startNetworkMonitoring() {
        if (window.ABXManager && window.ABXManager.startNetworkMonitoring) {
            window.ABXManager.startNetworkMonitoring();
        }
    }
    
    /**
     * اختبار سرعة الشبكة
     */
    performNetworkSpeedTest() {
        if (window.ABXManager && window.ABXManager.performNetworkSpeedTest) {
            window.ABXManager.performNetworkSpeedTest();
        }
    }
    
    /**
     * الحصول على معلومات التخزين
     */
    getStorageInfo() {
        if (window.ABXManager && window.ABXManager.getStorageInfo) {
            window.ABXManager.getStorageInfo();
        }
    }
    
    /**
     * تنظيف الكاش المتقدم
     */
    performAdvancedCacheCleanup() {
        if (window.ABXManager && window.ABXManager.performAdvancedCacheCleanup) {
            window.ABXManager.performAdvancedCacheCleanup();
        }
    }
    
    /**
     * إنشاء تقرير النظام
     */
    generateSystemReport() {
        if (window.ABXManager && window.ABXManager.generateSystemReport) {
            window.ABXManager.generateSystemReport();
        }
    }
    
    /**
     * تصدير السجلات
     */
    exportLogs(format = 'json') {
        if (window.ABXManager && window.ABXManager.exportLogs) {
            window.ABXManager.exportLogs(format);
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════════════
    //                              UI Helper Methods
    // ═══════════════════════════════════════════════════════════════════════════════════════
    
    /**
     * عرض إشعار
     */
    showNotification(title, message, type = 'info') {
        // إنشاء إشعار بسيط
        const notification = document.createElement('div');
        notification.className = `abx-notification abx-${type}`;
        notification.innerHTML = `
            <div class="abx-notification-header">
                <strong>${title}</strong>
                <button class="abx-notification-close">&times;</button>
            </div>
            <div class="abx-notification-body">${message}</div>
        `;
        
        // إضافة الأنماط إذا لم تكن موجودة
        this.ensureStyles();
        
        // إضافة الإشعار للصفحة
        document.body.appendChild(notification);
        
        // إضافة معالج الإغلاق
        notification.querySelector('.abx-notification-close').onclick = () => {
            notification.remove();
        };
        
        // إزالة تلقائية بعد 5 ثواني
        setTimeout(() => {
            if (notification.parentNode) {
                notification.remove();
            }
        }, 5000);
    }
    
    /**
     * تحديث لوحة النظام
     */
    updateSystemDashboard(data) {
        // البحث عن عنصر لوحة النظام
        let dashboard = document.getElementById('abx-dashboard');
        if (!dashboard) {
            dashboard = this.createSystemDashboard();
        }
        
        // تحديث البيانات
        if (data.memory) {
            const memoryElement = dashboard.querySelector('.abx-memory-info');
            if (memoryElement) {
                const usedPercent = ((data.memory.used / data.memory.total) * 100).toFixed(1);
                memoryElement.innerHTML = `Memory: ${usedPercent}% (${this.formatBytes(data.memory.used)} / ${this.formatBytes(data.memory.total)})`;
            }
        }
        
        if (data.battery) {
            const batteryElement = dashboard.querySelector('.abx-battery-info');
            if (batteryElement) {
                batteryElement.innerHTML = `Battery: ${data.battery.level}% ${data.battery.charging ? '(Charging)' : ''}`;
            }
        }
        
        if (data.network) {
            const networkElement = dashboard.querySelector('.abx-network-info');
            if (networkElement) {
                networkElement.innerHTML = `Network: ${data.network.connected ? 'Connected' : 'Disconnected'}`;
            }
        }
    }
    
    /**
     * إنشاء لوحة النظام
     */
    createSystemDashboard() {
        const dashboard = document.createElement('div');
        dashboard.id = 'abx-dashboard';
        dashboard.className = 'abx-dashboard';
        dashboard.innerHTML = `
            <div class="abx-dashboard-header">
                <h3>ABX System Monitor</h3>
                <button class="abx-dashboard-toggle">−</button>
            </div>
            <div class="abx-dashboard-content">
                <div class="abx-memory-info">Memory: Loading...</div>
                <div class="abx-battery-info">Battery: Loading...</div>
                <div class="abx-network-info">Network: Loading...</div>
                <div class="abx-dashboard-controls">
                    <button onclick="ABXClient.init().getSystemReport()">Refresh</button>
                    <button onclick="ABXClient.init().performAdvancedCacheCleanup()">Clean Cache</button>
                </div>
            </div>
        `;
        
        // إضافة معالج التبديل
        dashboard.querySelector('.abx-dashboard-toggle').onclick = () => {
            const content = dashboard.querySelector('.abx-dashboard-content');
            const toggle = dashboard.querySelector('.abx-dashboard-toggle');
            if (content.style.display === 'none') {
                content.style.display = 'block';
                toggle.textContent = '−';
            } else {
                content.style.display = 'none';
                toggle.textContent = '+';
            }
        };
        
        document.body.appendChild(dashboard);
        return dashboard;
    }
    
    /**
     * تأكد من وجود الأنماط
     */
    ensureStyles() {
        if (document.getElementById('abx-styles')) return;
        
        const styles = document.createElement('style');
        styles.id = 'abx-styles';
        styles.textContent = `
            .abx-notification {
                position: fixed;
                top: 20px;
                right: 20px;
                background: white;
                border-radius: 8px;
                box-shadow: 0 4px 12px rgba(0,0,0,0.15);
                min-width: 300px;
                z-index: 10000;
                animation: abxSlideIn 0.3s ease-out;
            }
            
            .abx-notification.abx-success { border-left: 4px solid #4CAF50; }
            .abx-notification.abx-warning { border-left: 4px solid #FF9800; }
            .abx-notification.abx-error { border-left: 4px solid #F44336; }
            .abx-notification.abx-info { border-left: 4px solid #2196F3; }
            
            .abx-notification-header {
                padding: 12px 16px 8px;
                display: flex;
                justify-content: space-between;
                align-items: center;
                border-bottom: 1px solid #eee;
            }
            
            .abx-notification-body {
                padding: 8px 16px 12px;
                color: #666;
            }
            
            .abx-notification-close {
                background: none;
                border: none;
                font-size: 18px;
                cursor: pointer;
                color: #999;
            }
            
            .abx-dashboard {
                position: fixed;
                bottom: 20px;
                left: 20px;
                background: white;
                border-radius: 8px;
                box-shadow: 0 4px 12px rgba(0,0,0,0.15);
                min-width: 250px;
                z-index: 9999;
            }
            
            .abx-dashboard-header {
                padding: 12px 16px;
                background: #f5f5f5;
                border-radius: 8px 8px 0 0;
                display: flex;
                justify-content: space-between;
                align-items: center;
            }
            
            .abx-dashboard-header h3 {
                margin: 0;
                font-size: 14px;
                color: #333;
            }
            
            .abx-dashboard-toggle {
                background: none;
                border: none;
                font-size: 16px;
                cursor: pointer;
                color: #666;
            }
            
            .abx-dashboard-content {
                padding: 12px 16px;
            }
            
            .abx-dashboard-content > div {
                margin-bottom: 8px;
                font-size: 12px;
                color: #666;
            }
            
            .abx-dashboard-controls {
                margin-top: 12px;
                display: flex;
                gap: 8px;
            }
            
            .abx-dashboard-controls button {
                padding: 4px 8px;
                border: 1px solid #ddd;
                background: white;
                border-radius: 4px;
                cursor: pointer;
                font-size: 11px;
            }
            
            .abx-dashboard-controls button:hover {
                background: #f5f5f5;
            }
            
            @keyframes abxSlideIn {
                from { transform: translateX(100%); opacity: 0; }
                to { transform: translateX(0); opacity: 1; }
            }
        `;
        
        document.head.appendChild(styles);
    }
    
    /**
     * تنسيق البايتات
     */
    formatBytes(bytes) {
        if (bytes === 0) return '0 B';
        const k = 1024;
        const sizes = ['B', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════════════
    //                              Utility Methods
    // ═══════════════════════════════════════════════════════════════════════════════════════
    
    /**
     * فحص توفر ABX
     */
    isABXAvailable() {
        return !!(window.ABXManager && typeof window.ABXManager === 'object');
    }
    
    /**
     * الحصول على حالة المراقبة
     */
    isMonitoringActive() {
        return this.monitoringActive;
    }
    
    /**
     * الحصول على آخر تقرير نظام
     */
    getLastSystemReport() {
        return this.lastSystemReport;
    }
}

// تهيئة تلقائية عند تحميل الصفحة
if (typeof window !== 'undefined') {
    window.ABXClient = ABXClient;
    
    // تهيئة تلقائية
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            ABXClient.init();
        });
    } else {
        ABXClient.init();
    }
}

// تصدير للاستخدام مع module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = ABXClient;
}