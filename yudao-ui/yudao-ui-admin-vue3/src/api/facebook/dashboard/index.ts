import request from '@/config/axios'

export interface FbDashboardSummary {
  headline: string
  subline: string
  leadCount: number
  highIntentCount: number
  recommendedCount: number
}

export interface FbDashboardMetricCard {
  key: string
  title: string
  value: number
  delta: number
  deltaLabel: string
  routePath: string
}

export interface FbDashboardRecommendedLead {
  id: number
  customerName: string
  source: string
  intentLevel: string
  aiReason: string
  recommendedAction: string
  targetUrl?: string
}

export interface FbDashboardAutomationItem {
  title: string
  value: number
  description: string
}

export interface FbDashboardTodoItem {
  title: string
  count: number
  level: string
  routePath: string
}

export interface FbDashboardAutomationAndTodos {
  automationItems: FbDashboardAutomationItem[]
  todoItems: FbDashboardTodoItem[]
}

export interface FbDashboardHomeRespVO {
  summary: FbDashboardSummary
  metrics: FbDashboardMetricCard[]
  recommendedLeads: FbDashboardRecommendedLead[]
  automationAndTodos: FbDashboardAutomationAndTodos
}

export const FbDashboardApi = {
  getHome: async (): Promise<FbDashboardHomeRespVO> => {
    return await request.get({ url: '/facebook/dashboard/home' })
  }
}
