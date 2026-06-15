// Mirrors the .NET DTOs in Struct.BLL.Core.Recommendation.Models (camelCased by ASP.NET JSON).

export type Purpose = 'Gaming' | 'Work' | 'Office'

export interface RecommendationRequest {
  budget: number
  purpose: Purpose
}

export interface ComponentDto {
  id: number
  name: string
  category: string
  brand: string
  price: number
  technicalSpecs: Record<string, string>
}

export interface RankedComponent {
  rank: number
  performanceScore: number // 0..1, sqrt-curved by the scorer
  component: ComponentDto
}

export interface SlotRecommendation {
  category: string
  allocatedBudget: number
  recommendations: RankedComponent[]
}

export interface FailedSlot {
  category: string
  reason: string
}

export interface RecommendationResult {
  purpose: string
  totalBudget: number
  actualTotalPrice: number
  isSuccess: boolean
  message: string
  slots: SlotRecommendation[]
  failedSlots: FailedSlot[]
}
