window.rollAndPaintSeo = {
    apply(metadata) {
        if (!metadata) {
            return;
        }

        const upsertMeta = (selector, attributes) => {
            let element = document.head.querySelector(selector);
            if (!element) {
                element = document.createElement('meta');
                document.head.appendChild(element);
            }

            Object.entries(attributes).forEach(([name, value]) => element.setAttribute(name, value));
        };

        const upsertLink = (selector, attributes) => {
            let element = document.head.querySelector(selector);
            if (!element) {
                element = document.createElement('link');
                document.head.appendChild(element);
            }

            Object.entries(attributes).forEach(([name, value]) => element.setAttribute(name, value));
        };

        const title = metadata.title || document.title || 'Roll & Paint | Miniature Painting Community';
        document.title = title;
        upsertMeta('meta[name="description"]', { name: 'description', content: metadata.description });
        upsertMeta('meta[name="robots"]', { name: 'robots', content: metadata.robots });
        upsertLink('link[rel="canonical"]', { rel: 'canonical', href: metadata.canonical });
        upsertMeta('meta[property="og:type"]', { property: 'og:type', content: metadata.openGraphType || 'website' });
        upsertMeta('meta[property="og:url"]', { property: 'og:url', content: metadata.canonical });
        upsertMeta('meta[property="og:title"]', { property: 'og:title', content: title });
        upsertMeta('meta[property="og:description"]', { property: 'og:description', content: metadata.description });
        upsertMeta('meta[name="twitter:title"]', { name: 'twitter:title', content: title });
        upsertMeta('meta[name="twitter:description"]', { name: 'twitter:description', content: metadata.description });
    }
};
