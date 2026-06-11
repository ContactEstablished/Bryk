import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '@/views/HomeView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'home',
      component: HomeView,
    },
    {
      path: '/onboarding',
      name: 'onboarding',
      component: () => import('@/views/OnboardingView.vue'),
    },
    {
      path: '/profile',
      name: 'profile',
      component: () => import('@/views/ProfileView.vue'),
    },
    {
      path: '/training',
      name: 'training',
      component: () => import('@/views/TrainingView.vue'),
    },
    {
      path: '/plans',
      name: 'plans',
      component: () => import('@/views/PlansView.vue'),
    },
    {
      path: '/plans/:id',
      name: 'plan-detail',
      component: () => import('@/views/PlanDetailView.vue'),
    },
    {
      path: '/workouts',
      name: 'workouts',
      component: () => import('@/views/WorkoutsView.vue'),
    },
    {
      path: '/workouts/:id',
      name: 'workout-detail',
      component: () => import('@/views/WorkoutDetailView.vue'),
    },
    {
      path: '/zones',
      name: 'zones',
      component: () => import('@/views/ZonesView.vue'),
    },
  ],
})

export default router
