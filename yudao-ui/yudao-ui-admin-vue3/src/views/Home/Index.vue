<template>
  <ContentWrap class="ai-home">
    <el-skeleton :loading="loading" animated>
      <div class="hero-card">
        <div class="hero-copy">
          <div class="hero-title">AI获客作战中心</div>
          <div class="hero-headline">{{ dashboard.summary.headline }}</div>
          <div class="hero-subline">{{ dashboard.summary.subline }}</div>
        </div>
      </div>

      <el-row :gutter="16" class="metric-row">
        <el-col v-for="item in dashboard.metrics" :key="item.key" :xl="6" :lg="6" :md="12" :sm="12" :xs="24">
          <div class="metric-card cursor-pointer" @click="goTo(item.routePath)">
            <div class="metric-title">{{ item.title }}</div>
            <div class="metric-value">{{ item.value }}</div>
            <div class="metric-delta" :class="{ up: item.delta > 0, down: item.delta < 0 }">
              {{ item.deltaLabel }}
            </div>
          </div>
        </el-col>
      </el-row>

      <el-row :gutter="16" class="content-row">
        <el-col :xl="14" :lg="14" :md="24" :sm="24" :xs="24">
          <div class="panel-card">
            <div class="panel-header">
              <div>
                <div class="panel-title">推荐线索</div>
                <div class="panel-subtitle">系统已经帮你筛出今天最值得优先联系的客户</div>
              </div>
              <el-button text type="primary" @click="goTo('/facebook/agent')">查看全部</el-button>
            </div>

            <div v-if="dashboard.recommendedLeads.length" class="lead-list">
              <div v-for="lead in dashboard.recommendedLeads" :key="lead.id" class="lead-item">
                <div class="lead-main">
                  <div class="lead-name">{{ lead.customerName }}</div>
                  <div class="lead-meta">
                    <el-tag size="small" type="info">{{ lead.source }}</el-tag>
                    <el-tag size="small" :type="getIntentTagType(lead.intentLevel)">
                      {{ lead.intentLevel }}
                    </el-tag>
                  </div>
                </div>
                <div class="lead-reason">{{ lead.aiReason }}</div>
                <div class="lead-footer">
                  <span class="lead-action">{{ lead.recommendedAction }}</span>
                  <el-link v-if="lead.targetUrl" :href="lead.targetUrl" target="_blank" type="primary">
                    查看主页
                  </el-link>
                </div>
              </div>
            </div>

            <el-empty v-else description="暂时还没有推荐线索，AI 正在继续筛选中" />
          </div>
        </el-col>

        <el-col :xl="10" :lg="10" :md="24" :sm="24" :xs="24">
          <div class="panel-card split-panel">
            <div class="panel-header">
              <div>
                <div class="panel-title">自动化结果与待办</div>
                <div class="panel-subtitle">系统在持续干活，你只需要处理最关键的动作</div>
              </div>
            </div>

            <div class="sub-panel">
              <div class="sub-title">今日 AI 已完成</div>
              <div class="stat-list">
                <div v-for="item in dashboard.automationAndTodos.automationItems" :key="item.title" class="stat-item">
                  <div>
                    <div class="stat-title">{{ item.title }}</div>
                    <div class="stat-desc">{{ item.description }}</div>
                  </div>
                  <div class="stat-value">{{ item.value }}</div>
                </div>
              </div>
            </div>

            <div class="sub-panel todo-panel">
              <div class="sub-title">当前待处理</div>
              <div class="todo-list">
                <div
                  v-for="item in dashboard.automationAndTodos.todoItems"
                  :key="item.title"
                  class="todo-item cursor-pointer"
                  @click="goTo(item.routePath)"
                >
                  <div>
                    <div class="todo-title">{{ item.title }}</div>
                    <div class="todo-level" :class="item.level">{{ getTodoLevelLabel(item.level) }}</div>
                  </div>
                  <div class="todo-count">{{ item.count }}</div>
                </div>
              </div>
            </div>
          </div>
        </el-col>
      </el-row>
    </el-skeleton>
  </ContentWrap>
</template>

<script lang="ts" setup>
import { useRouter } from 'vue-router'
import {
  FbDashboardApi,
  type FbDashboardHomeRespVO
} from '@/api/facebook/dashboard'

defineOptions({ name: 'Index' })

const router = useRouter()
const loading = ref(true)

const createDefaultDashboard = (): FbDashboardHomeRespVO => ({
  summary: {
    headline: '今天 AI 新增 0 条线索，筛出 0 位高意向客户，推荐优先联系 0 人',
    subline: '当前还没有可展示的数据，系统开始产出后会自动显示在这里。',
    leadCount: 0,
    highIntentCount: 0,
    recommendedCount: 0
  },
  metrics: [],
  recommendedLeads: [],
  automationAndTodos: {
    automationItems: [],
    todoItems: []
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

const goTo = (path?: string) => {
  if (!path) return
  router.push(path)
}

const getIntentTagType = (level: string) => {
  if (level.includes('高')) return 'danger'
  if (level.includes('中')) return 'warning'
  return 'info'
}

const getTodoLevelLabel = (level: string) => {
  if (level === 'high') return '优先处理'
  if (level === 'warning') return '需要关注'
  return '建议处理'
}

onMounted(() => {
  loadDashboard()
})
</script>

<style lang="scss" scoped>
.ai-home {
  :deep(.el-skeleton__item) {
    border-radius: 20px;
  }
}

.hero-card {
  padding: 28px;
  border-radius: 24px;
  background:
    radial-gradient(circle at top right, rgb(255 216 168 / 95%), transparent 32%),
    linear-gradient(135deg, #fff8ef 0%, #fff3df 45%, #ffe5c1 100%);
  border: 1px solid #f6d7a8;
}

.hero-title {
  font-size: 13px;
  font-weight: 700;
  letter-spacing: 0.08em;
  color: #9a5b14;
  text-transform: uppercase;
}

.hero-headline {
  margin-top: 10px;
  font-size: 28px;
  font-weight: 700;
  line-height: 1.35;
  color: #2d1804;
}

.hero-subline {
  margin-top: 8px;
  font-size: 14px;
  line-height: 1.7;
  color: #7b5730;
}

.metric-row,
.content-row {
  margin-top: 16px;
}

.metric-card,
.panel-card {
  border: 1px solid #ebe4d8;
  border-radius: 20px;
  background: #fffdfa;
  box-shadow: 0 14px 32px rgb(112 74 18 / 7%);
}

.metric-card {
  padding: 20px;
  transition:
    transform 0.2s ease,
    box-shadow 0.2s ease;
}

.metric-card:hover,
.todo-item:hover,
.lead-item:hover {
  transform: translateY(-2px);
}

.metric-title {
  font-size: 14px;
  color: #826040;
}

.metric-value {
  margin-top: 12px;
  font-size: 32px;
  font-weight: 700;
  color: #221506;
}

.metric-delta {
  margin-top: 10px;
  font-size: 13px;
  color: #8d7964;
}

.metric-delta.up {
  color: #1f8f55;
}

.metric-delta.down {
  color: #d74c3c;
}

.panel-card {
  padding: 22px;
  height: 100%;
}

.panel-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.panel-title {
  font-size: 20px;
  font-weight: 700;
  color: #221506;
}

.panel-subtitle {
  margin-top: 6px;
  font-size: 13px;
  color: #8b6b4d;
}

.lead-list,
.stat-list,
.todo-list {
  margin-top: 18px;
}

.lead-item {
  padding: 16px 0;
  border-bottom: 1px solid #f0e7dc;
  transition: transform 0.2s ease;
}

.lead-item:last-child {
  padding-bottom: 0;
  border-bottom: none;
}

.lead-main,
.lead-footer,
.stat-item,
.todo-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.lead-name {
  font-size: 16px;
  font-weight: 600;
  color: #2c1d0b;
}

.lead-meta {
  display: flex;
  gap: 8px;
  margin-top: 8px;
}

.lead-reason {
  margin-top: 10px;
  font-size: 13px;
  line-height: 1.7;
  color: #6f5a44;
}

.lead-footer {
  margin-top: 12px;
}

.lead-action {
  font-size: 13px;
  font-weight: 600;
  color: #b25f17;
}

.split-panel {
  display: flex;
  flex-direction: column;
}

.sub-panel + .sub-panel {
  margin-top: 20px;
  padding-top: 20px;
  border-top: 1px solid #f0e7dc;
}

.sub-title {
  font-size: 16px;
  font-weight: 700;
  color: #2c1d0b;
}

.stat-item,
.todo-item {
  padding: 12px 0;
}

.stat-item + .stat-item,
.todo-item + .todo-item {
  border-top: 1px dashed #eee1d2;
}

.stat-title,
.todo-title {
  font-size: 14px;
  font-weight: 600;
  color: #2c1d0b;
}

.stat-desc,
.todo-level {
  margin-top: 6px;
  font-size: 12px;
  color: #8a755f;
}

.todo-level.high {
  color: #d74c3c;
}

.todo-level.warning {
  color: #b67516;
}

.stat-value,
.todo-count {
  min-width: 48px;
  text-align: right;
  font-size: 24px;
  font-weight: 700;
  color: #221506;
}

@media (max-width: 768px) {
  .hero-card,
  .metric-card,
  .panel-card {
    border-radius: 18px;
  }

  .hero-headline {
    font-size: 22px;
  }

  .metric-value,
  .stat-value,
  .todo-count {
    font-size: 24px;
  }
}
</style>
