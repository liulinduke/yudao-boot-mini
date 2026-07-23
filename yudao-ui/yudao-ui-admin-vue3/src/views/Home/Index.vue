<template>
  <ContentWrap class="ai-home">
    <el-skeleton :loading="loading" animated>
      <section class="section-block ai-result-panel">
        <div class="section-header">
          <div>
            <h2 class="section-title">AI 获客成果</h2>
            <p class="section-subtitle">今日 AI 已自动完成的获客工作</p>
          </div>
          <div class="section-actions">
            <span class="section-date">今日</span>
            <el-button
              class="refresh-button"
              :loading="loading"
              circle
              text
              title="刷新首页数据"
              @click="loadDashboard"
            >
              <Icon icon="ep:refresh" :size="16" />
            </el-button>
          </div>
        </div>

        <el-row :gutter="12" class="ai-result-row">
          <el-col
            v-for="item in aiResultItems"
            :key="item.key"
            :xl="6"
            :lg="6"
            :md="12"
            :sm="12"
            :xs="24"
          >
            <div class="ai-result-item">
              <div class="result-icon" :class="item.colorClass">
                <Icon :icon="item.icon" :size="20" />
              </div>
              <div class="result-content">
                <div class="result-title">{{ item.title }}</div>
                <div class="result-value">{{ item.value }}</div>
                <div class="result-description">{{ item.description }}</div>
              </div>
            </div>
          </el-col>
        </el-row>
      </section>

      <el-row :gutter="16" class="summary-row">
        <el-col :xl="12" :lg="12" :md="24" :sm="24" :xs="24">
          <section class="section-block summary-panel">
            <div class="section-header">
              <div>
                <h2 class="section-title">社媒采集</h2>
                <p class="section-subtitle">今日从各社交平台采集的数据汇总</p>
              </div>
              <div class="summary-total">
                <span>今日总量</span>
                <strong>{{ dashboard.socialCollection.total }}</strong>
              </div>
            </div>

            <div v-if="dashboard.socialCollection.items.length" class="summary-list">
              <div
                v-for="item in dashboard.socialCollection.items"
                :key="item.type"
                class="summary-item"
              >
                <span>{{ item.type }}</span>
                <strong>{{ item.count }}</strong>
              </div>
            </div>
            <el-empty v-else :image-size="56" description="今日暂无采集数据" />
          </section>
        </el-col>

        <el-col :xl="12" :lg="12" :md="24" :sm="24" :xs="24">
          <section class="section-block summary-panel">
            <div class="section-header">
              <div>
                <h2 class="section-title">社媒运营</h2>
                <p class="section-subtitle">今日各社交平台已执行的运营动作汇总</p>
              </div>
              <div class="summary-total">
                <span>今日总量</span>
                <strong>{{ dashboard.socialOperation.total }}</strong>
              </div>
            </div>

            <div v-if="dashboard.socialOperation.items.length" class="summary-list">
              <div
                v-for="item in dashboard.socialOperation.items"
                :key="item.type"
                class="summary-item"
              >
                <span>{{ item.type }}</span>
                <strong>{{ item.count }}</strong>
              </div>
            </div>
            <el-empty v-else :image-size="56" description="今日暂无运营数据" />
          </section>
        </el-col>
      </el-row>
    </el-skeleton>
  </ContentWrap>
</template>

<script lang="ts" setup>
import { FbDashboardApi, type FbDashboardHomeRespVO } from '@/api/facebook/dashboard'

defineOptions({ name: 'Index' })

const loading = ref(true)

const createDefaultDashboard = (): FbDashboardHomeRespVO => ({
  aiResult: {
    autoCollectedLeadCount: 0,
    autoAnalyzedCustomerCount: 0,
    generatedInteractionSuggestionCount: 0,
    autoTouchedCount: 0
  },
  socialCollection: {
    total: 0,
    items: []
  },
  socialOperation: {
    total: 0,
    items: []
  }
})

const dashboard = ref<FbDashboardHomeRespVO>(createDefaultDashboard())

const loadDashboard = async () => {
  loading.value = true
  try {
    dashboard.value = await FbDashboardApi.getHome()
  } finally {
    loading.value = false
  }
}

const aiResultItems = computed(() => [
  {
    key: 'collected',
    title: '自动发现线索',
    value: dashboard.value.aiResult.autoCollectedLeadCount,
    description: '今日新增线索',
    icon: 'ep:download',
    colorClass: 'blue'
  },
  {
    key: 'analyzed',
    title: '自动分析客户',
    value: dashboard.value.aiResult.autoAnalyzedCustomerCount,
    description: '完成 AI 判断与摘要',
    icon: 'ep:cpu',
    colorClass: 'violet'
  },
  {
    key: 'suggestions',
    title: '生成互动建议',
    value: dashboard.value.aiResult.generatedInteractionSuggestionCount,
    description: '私信或互动建议',
    icon: 'ep:chat-line-round',
    colorClass: 'orange'
  },
  {
    key: 'touched',
    title: '自动完成触达',
    value: dashboard.value.aiResult.autoTouchedCount,
    description: '成功执行的触达动作',
    icon: 'ep:promotion',
    colorClass: 'green'
  }
])

onMounted(() => {
  loadDashboard()
})
</script>

<style lang="scss" scoped>
.ai-home {
  :deep(.el-skeleton__item) {
    border-radius: 10px;
  }
}

.section-block {
  border: 1px solid #e8edf3;
  border-radius: 10px;
  background: #fff;
}

.ai-result-panel {
  padding: 24px;
}

.section-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}

.section-title {
  margin: 0;
  color: #172b4d;
  font-size: 19px;
  font-weight: 700;
  line-height: 1.4;
}

.section-subtitle {
  margin: 5px 0 0;
  color: #8492a6;
  font-size: 13px;
  line-height: 1.5;
}

.section-date {
  padding: 5px 10px;
  border-radius: 5px;
  background: #f0f5ff;
  color: #3f6fbd;
  font-size: 12px;
  white-space: nowrap;
}

.section-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  white-space: nowrap;
}

.refresh-button {
  width: 30px;
  height: 30px;
  margin: 0;
  color: #64748b;
}

.refresh-button:hover {
  color: #3478d4;
  background: #f0f5ff;
}

.ai-result-row {
  margin-top: 20px;
}

.ai-result-item {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  min-height: 132px;
  padding: 18px;
  border: 1px solid #edf1f5;
  border-radius: 8px;
  background: #fbfcfe;
}

.result-icon {
  display: flex;
  flex: 0 0 38px;
  align-items: center;
  justify-content: center;
  width: 38px;
  height: 38px;
  border-radius: 8px;
}

.result-icon.blue {
  background: #e9f2ff;
  color: #3478d4;
}

.result-icon.violet {
  background: #f0ebff;
  color: #7554c7;
}

.result-icon.orange {
  background: #fff1df;
  color: #c77820;
}

.result-icon.green {
  background: #e6f7ef;
  color: #26945d;
}

.result-content {
  min-width: 0;
}

.result-title {
  color: #31445f;
  font-size: 14px;
  font-weight: 600;
}

.result-value {
  margin-top: 10px;
  color: #172b4d;
  font-size: 30px;
  font-weight: 700;
  line-height: 1;
}

.result-description {
  margin-top: 10px;
  color: #8b98aa;
  font-size: 12px;
  line-height: 1.4;
}

.summary-row {
  margin-top: 16px;
}

.summary-panel {
  min-height: 300px;
  padding: 22px 24px;
}

.summary-total {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 3px;
  color: #8b98aa;
  font-size: 12px;
  white-space: nowrap;
}

.summary-total strong {
  color: #172b4d;
  font-size: 24px;
  line-height: 1.2;
}

.summary-list {
  margin-top: 20px;
}

.summary-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 42px;
  padding: 9px 0;
  border-bottom: 1px solid #f0f3f7;
  color: #52657f;
  font-size: 14px;
}

.summary-item:last-child {
  border-bottom: 0;
}

.summary-item strong {
  color: #172b4d;
  font-size: 17px;
}

@media (max-width: 768px) {
  .ai-result-panel,
  .summary-panel {
    padding: 18px;
  }

  .section-title {
    font-size: 17px;
  }

  .ai-result-item {
    min-height: 116px;
    padding: 14px;
  }

  .result-value {
    font-size: 26px;
  }
}
</style>
