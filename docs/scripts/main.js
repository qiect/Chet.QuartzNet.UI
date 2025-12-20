// 平滑滚动功能
document.addEventListener('DOMContentLoaded', function() {
    // 为所有导航链接添加平滑滚动
    const navLinks = document.querySelectorAll('.nav-link');
    
    navLinks.forEach(link => {
        link.addEventListener('click', function(e) {
            e.preventDefault();
            
            const targetId = this.getAttribute('href');
            const targetElement = document.querySelector(targetId);
            
            if (targetElement) {
                const offsetTop = targetElement.offsetTop - 80; // 考虑导航栏高度
                
                window.scrollTo({
                    top: offsetTop,
                    behavior: 'smooth'
                });
            }
        });
    });
    
    // 滚动时高亮当前导航链接
    window.addEventListener('scroll', function() {
        const sections = document.querySelectorAll('section[id], header[id]');
        const scrollPosition = window.scrollY + 100;
        
        sections.forEach(section => {
            const sectionTop = section.offsetTop;
            const sectionHeight = section.offsetHeight;
            const sectionId = section.getAttribute('id');
            
            if (scrollPosition >= sectionTop && scrollPosition < sectionTop + sectionHeight) {
                navLinks.forEach(link => {
                    link.classList.remove('active');
                    if (link.getAttribute('href') === `#${sectionId}`) {
                        link.classList.add('active');
                    }
                });
            }
        });
    });
    
    // 滚动动画
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };
    
    const observer = new IntersectionObserver(function(entries) {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('fade-in-up');
            }
        });
    }, observerOptions);
    
    // 观察所有部分
    const sections = document.querySelectorAll('section');
    sections.forEach(section => {
        observer.observe(section);
    });
    
    // 代码块复制功能
    const codeBlocks = document.querySelectorAll('.code-block pre');
    
    codeBlocks.forEach(codeBlock => {
        const copyButton = document.createElement('button');
        copyButton.textContent = '复制';
        copyButton.className = 'copy-button';
        copyButton.style.cssText = `
            position: absolute;
            top: 10px;
            right: 10px;
            padding: 5px 10px;
            background-color: #3498db;
            color: white;
            border: none;
            border-radius: 4px;
            font-size: 0.8rem;
            cursor: pointer;
            transition: background-color 0.3s ease;
        `;
        
        // 为代码块添加相对定位
        codeBlock.parentNode.style.position = 'relative';
        
        codeBlock.parentNode.appendChild(copyButton);
        
        copyButton.addEventListener('click', function() {
            const codeText = codeBlock.textContent;
            
            navigator.clipboard.writeText(codeText)
                .then(() => {
                    copyButton.textContent = '已复制';
                    copyButton.style.backgroundColor = '#27ae60';
                    
                    setTimeout(() => {
                        copyButton.textContent = '复制';
                        copyButton.style.backgroundColor = '#3498db';
                    }, 2000);
                })
                .catch(err => {
                    console.error('复制失败:', err);
                    copyButton.textContent = '复制失败';
                    copyButton.style.backgroundColor = '#e74c3c';
                    
                    setTimeout(() => {
                        copyButton.textContent = '复制';
                        copyButton.style.backgroundColor = '#3498db';
                    }, 2000);
                });
        });
    });
    
    // 导航栏滚动效果
    window.addEventListener('scroll', function() {
        const navbar = document.querySelector('.navbar');
        if (window.scrollY > 50) {
            navbar.style.backgroundColor = 'rgba(255, 255, 255, 0.95)';
            navbar.style.boxShadow = '0 2px 10px rgba(0, 0, 0, 0.1)';
        } else {
            navbar.style.backgroundColor = 'rgba(255, 255, 255, 1)';
            navbar.style.boxShadow = '0 2px 4px rgba(0, 0, 0, 0.1)';
        }
    });
    
    // 移动端菜单切换（如果需要）
    // 这里可以添加移动端菜单的切换逻辑
});

// 添加 CSS 样式用于复制按钮
const style = document.createElement('style');
style.textContent = `
    .copy-button {
        z-index: 10;
    }
    
    .nav-link.active {
        color: #3498db;
        font-weight: 600;
    }
    
    /* 滚动动画类 */
    .fade-in-up {
        animation: fadeInUp 0.6s ease-out;
    }
    
    @keyframes fadeInUp {
        from {
            opacity: 0;
            transform: translateY(30px);
        }
        to {
            opacity: 1;
            transform: translateY(0);
        }
    }
`;
document.head.appendChild(style);